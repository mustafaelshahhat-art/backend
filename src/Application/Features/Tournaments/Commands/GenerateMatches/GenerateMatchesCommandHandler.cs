using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.GenerateMatches;

public class GenerateMatchesCommandHandler : IRequestHandler<GenerateMatchesCommand, IEnumerable<MatchDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;

    public GenerateMatchesCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        ITournamentLifecycleService lifecycleService,
        IDistributedLock distributedLock,
        IMapper mapper,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _lifecycleService = lifecycleService;
        _distributedLock = distributedLock;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<IEnumerable<MatchDto>> Handle(GenerateMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية إنشاء المباريات قيد التنفيذ بالفعل.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            // Authorization
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            // 1. Registration is closed
            if (tournament.Status != TournamentStatus.RegistrationClosed)
            {
                throw new ConflictException("يجب إغلاق التسجيل قبل إنشاء المباريات.");
            }

            // PROD-FIX: Prevent auto-generation in Manual mode
            if (tournament.SchedulingMode == SchedulingMode.Manual)
            {
                throw new BadRequestException("لا يمكن استخدام التوليد التلقائي في وضع الجدولة اليدوية. يرجى استخدام أدوات الجدولة اليدوية.");
            }

            // 2. No existing matches already generated
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (existingMatches.Any())
            {
                throw new ConflictException("المباريات مولدة بالفعل لهذه البطولة.");
            }

            // 3. Opening teams validation (if defined, both must be present and registered)
            if (tournament.HasOpeningTeams)
            {
                var registrationsCheck = await _registrationRepository.FindAsync(
                    r => r.TournamentId == request.TournamentId && r.Status == RegistrationStatus.Approved, 
                    cancellationToken);
                var regIds = registrationsCheck.Select(r => r.TeamId).ToHashSet();

                if (!regIds.Contains(tournament.OpeningTeamAId!.Value))
                    throw new ConflictException("فريق المباراة الافتتاحية الأول لم يعد مسجلاً في البطولة.");
                if (!regIds.Contains(tournament.OpeningTeamBId!.Value))
                    throw new ConflictException("فريق المباراة الافتتاحية الثاني لم يعد مسجلاً في البطولة.");
            }

            // 4. Minimum team count
            var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.Status == RegistrationStatus.Approved, cancellationToken);
            var teamIds = registrations.Select(r => r.TeamId).ToList();
            
            int minRequired = tournament.MinTeams ?? 2;
            if (teamIds.Count < minRequired)
            {
                throw new ConflictException($"عدد الفرق غير كافٍ. المطلوب {minRequired} فريق على الأقل.");
            }

            // EXECUTION: MATCH GENERATION (with Opening Match seed protection)
            var matches = await CreateMatchesAsync(tournament, teamIds, cancellationToken);
            
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            var dtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
            await _notifier.SendMatchesGeneratedAsync(dtos, cancellationToken);

            return dtos;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }

    private async Task<List<Match>> CreateMatchesAsync(Tournament tournament, List<Guid> teamIds, CancellationToken ct)
    {
        var matches = new List<Match>();
        var random = new Random();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();
        
        // ============================================================
        // OPENING MATCH SEED PROTECTION
        // If opening teams are defined:
        //   1. Remove them from the shuffle pool
        //   2. Randomly select one group
        //   3. Insert both teams into that group first
        //   4. Shuffle remaining teams
        //   5. Fill remaining group slots normally
        // ============================================================
        
        if (effectiveMode == TournamentMode.GroupsKnockoutSingle || effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;
            
            var groups = new List<List<Guid>>();
            for (int i = 0; i < tournament.NumberOfGroups; i++) groups.Add(new List<Guid>());

            if (tournament.HasOpeningTeams)
            {
                // SEED PROTECTION: Remove opening teams from shuffle pool
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remainingTeams = teamIds.Where(id => id != openingTeamA && id != openingTeamB).ToList();

                // Randomly select a group for the opening teams
                int openingGroupIndex = random.Next(tournament.NumberOfGroups);
                groups[openingGroupIndex].Add(openingTeamA);
                groups[openingGroupIndex].Add(openingTeamB);

                // Shuffle remaining teams and distribute
                var shuffledRemaining = remainingTeams.OrderBy(x => random.Next()).ToList();
                for (int i = 0; i < shuffledRemaining.Count; i++)
                {
                    // Find the group with fewest teams (round-robin distribution)
                    int targetGroup = 0;
                    int minCount = groups[0].Count;
                    for (int g = 1; g < tournament.NumberOfGroups; g++)
                    {
                        if (groups[g].Count < minCount)
                        {
                            minCount = groups[g].Count;
                            targetGroup = g;
                        }
                    }
                    groups[targetGroup].Add(shuffledRemaining[i]);
                }
            }
            else
            {
                // Standard shuffle (no opening teams)
                var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
                for (int i = 0; i < shuffledTeams.Count; i++)
                {
                    groups[i % tournament.NumberOfGroups].Add(shuffledTeams[i]);
                }
            }
            
            int dayOffset = 0;
            bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                bool isOpeningGroup = tournament.HasOpeningTeams && 
                    groupTeams.Contains(tournament.OpeningTeamAId!.Value) && 
                    groupTeams.Contains(tournament.OpeningTeamBId!.Value);

                // Generate matches for this group
                var groupMatchList = new List<Match>();
                
                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                         var match = CreateMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage");
                         
                         // Mark opening match
                         if (isOpeningGroup && IsOpeningPair(tournament, groupTeams[i], groupTeams[j]))
                         {
                             match.IsOpeningMatch = true;
                         }
                         
                         groupMatchList.Add(match);
                         dayOffset++;
                         
                         if (isHomeAway)
                         {
                             groupMatchList.Add(CreateMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                             dayOffset++;
                         }
                    }
                }

                // OPENING MATCH FIRST: Reorder so the opening match is first in the group
                if (isOpeningGroup)
                {
                    var openingMatch = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
                    if (openingMatch != null)
                    {
                        groupMatchList.Remove(openingMatch);
                        // Swap dates: opening match gets the earliest date
                        if (groupMatchList.Count > 0)
                        {
                            var earliestDate = groupMatchList.Min(m => m.Date);
                            var openingOrigDate = openingMatch.Date;
                            var firstMatch = groupMatchList.FirstOrDefault(m => m.Date == earliestDate);
                            if (firstMatch != null && earliestDate < openingOrigDate)
                            {
                                firstMatch.Date = openingOrigDate;
                                openingMatch.Date = earliestDate;
                            }
                        }
                        groupMatchList.Insert(0, openingMatch);
                    }
                }

                matches.AddRange(groupMatchList);
            }
        }
        else if (effectiveMode == TournamentMode.KnockoutSingle || effectiveMode == TournamentMode.KnockoutHomeAway)
        {
            List<Guid> shuffledTeams;
            
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB)
                    .OrderBy(x => random.Next()).ToList();

                // Opening teams go first as a pair
                shuffledTeams = new List<Guid> { openingTeamA, openingTeamB };
                shuffledTeams.AddRange(remaining);
            }
            else
            {
                shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                 if (i + 1 < shuffledTeams.Count)
                 {
                     var match = CreateMatch(tournament, shuffledTeams[i], shuffledTeams[i+1], matchDate.AddDays(i), null, 1, "Round 1");
                     
                     // Mark opening match (first pair)
                     if (tournament.HasOpeningTeams && i == 0)
                     {
                         match.IsOpeningMatch = true;
                     }
                     
                     matches.Add(match);
                     
                     if (isHomeAway)
                     {
                          matches.Add(CreateMatch(tournament, shuffledTeams[i+1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1"));
                     }
                 }
            }
        }
        else // League modes
        {
            List<Guid> orderedTeams;
            
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB)
                    .OrderBy(x => random.Next()).ToList();

                // Place opening teams first
                orderedTeams = new List<Guid> { openingTeamA, openingTeamB };
                orderedTeams.AddRange(remaining);
            }
            else
            {
                orderedTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.LeagueHomeAway;
            int matchCount = 0;
            bool openingMatchSet = false;
            
            for (int i = 0; i < orderedTeams.Count; i++)
            {
                for (int j = i + 1; j < orderedTeams.Count; j++)
                {
                    var match = CreateMatch(tournament, orderedTeams[i], orderedTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League");
                    
                    // Mark opening match
                    if (!openingMatchSet && tournament.HasOpeningTeams && IsOpeningPair(tournament, orderedTeams[i], orderedTeams[j]))
                    {
                        match.IsOpeningMatch = true;
                        openingMatchSet = true;
                        
                        // Move this match to earliest date
                        if (matchCount > 0)
                        {
                            var firstDate = matchDate;
                            var currentDate = match.Date;
                            // Find the first match and swap dates
                            if (matches.Count > 0)
                            {
                                matches[0].Date = currentDate;
                                match.Date = firstDate;
                            }
                            matches.Insert(0, match);
                            matchCount++;
                            
                            if (isHomeAway)
                            {
                                matches.Add(CreateMatch(tournament, orderedTeams[j], orderedTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                                matchCount++;
                            }
                            continue;
                        }
                    }
                    
                    matches.Add(match);
                    matchCount++;
                    
                    if (isHomeAway)
                    {
                        matches.Add(CreateMatch(tournament, orderedTeams[j], orderedTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                        matchCount++;
                    }
                }
            }
        }
        
        await _matchRepository.AddRangeAsync(matches, ct);
        return matches;
    }

    /// <summary>
    /// Check if two team IDs form the opening pair (in either order).
    /// </summary>
    private bool IsOpeningPair(Tournament tournament, Guid teamId1, Guid teamId2)
    {
        if (!tournament.HasOpeningTeams) return false;
        var a = tournament.OpeningTeamAId!.Value;
        var b = tournament.OpeningTeamBId!.Value;
        return (teamId1 == a && teamId2 == b) || (teamId1 == b && teamId2 == a);
    }

    private Match CreateMatch(Tournament t, Guid h, Guid a, DateTime d, int? g, int r, string s)
    {
        return new Match
        {
            TournamentId = t.Id,
            HomeTeamId = h,
            AwayTeamId = a,
            Date = d,
            GroupId = g,
            RoundNumber = r,
            StageName = s,
            Status = MatchStatus.Scheduled,
            HomeScore = 0,
            AwayScore = 0
        };
    }
}

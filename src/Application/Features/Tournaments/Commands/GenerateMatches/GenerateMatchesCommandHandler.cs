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
        var lockKey = $"tournament:generate:{request.TournamentId}";
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

            // 2. No existing matches already generated
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (existingMatches.Any())
            {
                throw new ConflictException("المباريات مولدة بالفعل لهذه البطولة.");
            }

            // 3. Minimum team count
            var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.Status == RegistrationStatus.Approved, cancellationToken);
            var teamIds = registrations.Select(r => r.TeamId).ToList();
            
            int minRequired = tournament.MinTeams ?? 2;
            if (teamIds.Count < minRequired)
            {
                throw new ConflictException($"عدد الفرق غير كافٍ. المطلوب {minRequired} فريق على الأقل.");
            }

            // EXECUTION: MATCH GENERATION
            var matches = await CreateMatchesAsync(tournament, teamIds, cancellationToken);
            
            // Guarded Transition
            tournament.ChangeStatus(TournamentStatus.Active);
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
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();
        
        if (effectiveMode == TournamentMode.GroupsKnockoutSingle || effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;
            
            var groups = new List<List<Guid>>();
            for (int i = 0; i < tournament.NumberOfGroups; i++) groups.Add(new List<Guid>());
            
            for (int i = 0; i < shuffledTeams.Count; i++)
            {
                groups[i % tournament.NumberOfGroups].Add(shuffledTeams[i]);
            }
            
            int dayOffset = 0;
            bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                         matches.Add(CreateMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage"));
                         dayOffset++;
                         
                         if (isHomeAway)
                         {
                             matches.Add(CreateMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                             dayOffset++;
                         }
                    }
                }
            }
        }
        else if (effectiveMode == TournamentMode.KnockoutSingle || effectiveMode == TournamentMode.KnockoutHomeAway)
        {
            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                 if (i + 1 < shuffledTeams.Count)
                 {
                     matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[i+1], matchDate.AddDays(i), null, 1, "Round 1"));
                     
                     if (isHomeAway)
                     {
                          matches.Add(CreateMatch(tournament, shuffledTeams[i+1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1"));
                     }
                 }
            }
        }
        else // League modes
        {
             bool isHomeAway = effectiveMode == TournamentMode.LeagueHomeAway;
             int matchCount = 0;
             for (int i = 0; i < shuffledTeams.Count; i++)
             {
                for (int j = i + 1; j < shuffledTeams.Count; j++)
                {
                    matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League"));
                    matchCount++;
                    
                    if (isHomeAway)
                    {
                        matches.Add(CreateMatch(tournament, shuffledTeams[j], shuffledTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                        matchCount++;
                    }
                }
             }
        }
        
        await _matchRepository.AddRangeAsync(matches, ct);
        return matches;
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

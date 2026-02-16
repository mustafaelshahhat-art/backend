using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.ManualDraw;

public class ManualDrawCommandHandler : IRequestHandler<ManualDrawCommand, IEnumerable<MatchDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedLock _distributedLock;

    public ManualDrawCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _distributedLock = distributedLock;
    }

    public async Task<IEnumerable<MatchDto>> Handle(ManualDrawCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-matches-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(15)))
        {
            throw new ConflictException("يتم معالجة مباريات هذه البطولة من قبل مستخدم آخر حالياً.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, new[] { "Registrations" }, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            // Authorization
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            if (tournament.Status != TournamentStatus.RegistrationClosed && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
            {
                throw new BadRequestException("يجب إغلاق التسجيل قبل إنشاء المباريات.");
            }

            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل. قم بحذفها أولاً لإعادة التوليد.");

            // Fetch ALL approved teams for validation
            var approvedRegistrations = tournament.Registrations
                .Where(r => r.Status == RegistrationStatus.Approved)
                .ToList();
            var registeredTeamIds = approvedRegistrations.Select(r => r.TeamId).ToHashSet();

            var matches = new List<Match>();
            var matchDate = DateTime.UtcNow.AddDays(2);
            var effectiveMode = tournament.GetEffectiveMode();

            if (request.Request.GroupAssignments != null && request.Request.GroupAssignments.Any())
            {
                // ── GROUP ASSIGNMENT VALIDATION ──
                var allTeamIds = request.Request.GroupAssignments.SelectMany(a => a.TeamIds).ToList();

                // All teams must be registered & approved
                if (!allTeamIds.All(id => registeredTeamIds.Contains(id)))
                    throw new BadRequestException("بعض الفرق المحددة غير مسجلة أو لم يتم الموافقة عليها.");

                // No duplicates across groups
                if (allTeamIds.Count != allTeamIds.Distinct().Count())
                    throw new BadRequestException("لا يمكن تكرار الفريق في أكثر من مجموعة.");

                // All approved teams must be assigned
                if (allTeamIds.Count != registeredTeamIds.Count)
                    throw new BadRequestException("يجب تعيين جميع الفرق المسجلة في المجموعات.");

                // Group count validation
                if (request.Request.GroupAssignments.Count != tournament.NumberOfGroups)
                    throw new BadRequestException($"يجب تعيين الفرق لعدد {tournament.NumberOfGroups} مجموعة بالضبط.");

                foreach (var group in request.Request.GroupAssignments)
                {
                    var teams = group.TeamIds;
                    bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway || effectiveMode == TournamentMode.LeagueHomeAway;

                    for (int i = 0; i < teams.Count; i++)
                    {
                        for (int j = i + 1; j < teams.Count; j++)
                        {
                            matches.Add(CreateMatch(tournament, teams[i], teams[j], matchDate, group.GroupId, 1, "Group Stage"));
                            matchDate = matchDate.AddHours(2);
                            if (isHomeAway)
                            {
                                matches.Add(CreateMatch(tournament, teams[j], teams[i], matchDate, group.GroupId, 1, "Group Stage"));
                                matchDate = matchDate.AddHours(2);
                            }
                        }
                    }
                }
            }
            else if (request.Request.KnockoutPairings != null && request.Request.KnockoutPairings.Any())
            {
                // ── KNOCKOUT PAIRING VALIDATION ──
                var participantIds = request.Request.KnockoutPairings
                    .SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).ToList();

                if (!participantIds.All(id => registeredTeamIds.Contains(id)))
                    throw new BadRequestException("بعض الفرق المحددة غير مسجلة.");

                if (participantIds.Count != participantIds.Distinct().Count())
                    throw new BadRequestException("لا يمكن تكرار الفريق في المواجهات.");

                if (participantIds.Count != registeredTeamIds.Count)
                    throw new BadRequestException("يجب تضمين جميع الفرق المسجلة في المواجهات.");

                foreach (var pairing in request.Request.KnockoutPairings)
                {
                    if (pairing.HomeTeamId == pairing.AwayTeamId)
                        throw new BadRequestException("لا يمكن للفريق أن يواجه نفسه.");
                }

                bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
                foreach (var pairing in request.Request.KnockoutPairings)
                {
                    matches.Add(CreateMatch(tournament, pairing.HomeTeamId, pairing.AwayTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName));
                    matchDate = matchDate.AddHours(2);
                    if (isHomeAway)
                    {
                        matches.Add(CreateMatch(tournament, pairing.AwayTeamId, pairing.HomeTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName));
                        matchDate = matchDate.AddHours(2);
                    }
                }
            }

            await _matchRepository.AddRangeAsync(matches, cancellationToken);
        
            // Guarded Transition
            tournament.ChangeStatus(TournamentStatus.Active);
        
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return _mapper.Map<IEnumerable<MatchDto>>(matches);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
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

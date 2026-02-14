using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.GenerateManualGroupMatches;

public class GenerateManualGroupMatchesCommandHandler : IRequestHandler<GenerateManualGroupMatchesCommand, IEnumerable<MatchDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;

    public GenerateManualGroupMatchesCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IDistributedLock distributedLock,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _distributedLock = distributedLock;
        _mapper = mapper;
    }

    public async Task<IEnumerable<MatchDto>> Handle(GenerateManualGroupMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية جدولة أخرى قيد التنفيذ لهذا الدوري.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null) throw new NotFoundException("البطولة غير موجودة.");

            if (request.UserRole != UserRole.Admin.ToString() && tournament.CreatorUserId != request.UserId)
            {
                throw new ForbiddenException("ليس لديك صلاحية لتعديل جدولة هذه البطولة.");
            }

            if (tournament.SchedulingMode != SchedulingMode.Manual)
            {
                throw new BadRequestException("البطولة ليست في وضع الجدولة اليدوية.");
            }

            var matchesExist = await _matchRepository.AnyAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (matchesExist)
            {
                throw new BadRequestException("المباريات موجودة بالفعل.");
            }

            var allApprovedRegistrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId && r.Status == RegistrationStatus.Approved,
                cancellationToken);

            if (allApprovedRegistrations.Any(r => r.GroupId == null))
            {
                throw new BadRequestException("لم يتم تعيين جميع الفرق للمجموعات.");
            }

            var groups = allApprovedRegistrations.GroupBy(r => r.GroupId!.Value).ToList();
            if (groups.Count != tournament.NumberOfGroups)
            {
                throw new BadRequestException("عدد المجموعات غير مكتمل.");
            }

            // ============================================================
            // SECTION 4/5: Opening teams validation for manual mode
            // ============================================================
            if (tournament.HasOpeningTeams)
            {
                var openingA = tournament.OpeningTeamAId!.Value;
                var openingB = tournament.OpeningTeamBId!.Value;

                var groupOfA = allApprovedRegistrations.FirstOrDefault(r => r.TeamId == openingA)?.GroupId;
                var groupOfB = allApprovedRegistrations.FirstOrDefault(r => r.TeamId == openingB)?.GroupId;

                if (groupOfA == null || groupOfB == null)
                    throw new BadRequestException("فريقا المباراة الافتتاحية غير مُعَيّنَين في المجموعات.");

                if (groupOfA != groupOfB)
                    throw new BadRequestException("فريقا المباراة الافتتاحية يجب أن يكونا في نفس المجموعة.");
            }

            var matches = new List<Match>();
            var matchDate = tournament.StartDate.AddHours(18);

            foreach (var group in groups)
            {
                var teamIds = group.Select(r => r.TeamId).ToList();
                var groupMatches = GenerateRoundRobin(tournament, teamIds, group.Key, ref matchDate);
                matches.AddRange(groupMatches);
            }

            // ============================================================
            // SECTION 5: Opening match must be first match in its group
            // ============================================================
            if (tournament.HasOpeningTeams)
            {
                var openingMatch = matches.FirstOrDefault(m => m.IsOpeningMatch);
                if (openingMatch != null)
                {
                    // Find the first match overall and swap dates if needed
                    var firstMatch = matches.OrderBy(m => m.Date).First();
                    if (firstMatch.Id != openingMatch.Id)
                    {
                        var tempDate = firstMatch.Date;
                        firstMatch.Date = openingMatch.Date;
                        openingMatch.Date = tempDate;
                    }
                }
            }

            await _matchRepository.AddRangeAsync(matches, cancellationToken);
            return _mapper.Map<IEnumerable<MatchDto>>(matches);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }

    private List<Match> GenerateRoundRobin(Tournament tournament, List<Guid> teamIds, int groupId, ref DateTime matchDate)
    {
        var matches = new List<Match>();
        int n = teamIds.Count;
        if (n < 2) return matches;

        // Simple Round Robin algorithm (Circle Method)
        // For manual mode, we respect the order provided.
        for (int round = 0; round < n - 1; round++)
        {
            for (int i = 0; i < n / 2; i++)
            {
                int home = (round + i) % (n - 1);
                int away = (n - 1 - i + round) % (n - 1);

                if (i == 0) away = n - 1;

                var match = new Match
                {
                    TournamentId = tournament.Id,
                    HomeTeamId = teamIds[home],
                    AwayTeamId = teamIds[away],
                    GroupId = groupId,
                    RoundNumber = round + 1,
                    StageName = "دور المجموعات",
                    Status = MatchStatus.Scheduled,
                    Date = matchDate
                };

                // Mark opening match
                if (tournament.HasOpeningTeams && IsOpeningPair(tournament, teamIds[home], teamIds[away]))
                {
                    match.IsOpeningMatch = true;
                }

                matches.Add(match);
                matchDate = matchDate.AddHours(2);
            }
        }

        // Reorder: Opening match first in its group
        var opening = matches.FirstOrDefault(m => m.IsOpeningMatch);
        if (opening != null && matches.IndexOf(opening) > 0)
        {
            matches.Remove(opening);
            var earliestDate = matches[0].Date;
            var openingDate = opening.Date;
            matches[0].Date = openingDate;
            opening.Date = earliestDate;
            matches.Insert(0, opening);
        }

        return matches;
    }

    private bool IsOpeningPair(Tournament tournament, Guid teamId1, Guid teamId2)
    {
        if (!tournament.HasOpeningTeams) return false;
        var a = tournament.OpeningTeamAId!.Value;
        var b = tournament.OpeningTeamBId!.Value;
        return (teamId1 == a && teamId2 == b) || (teamId1 == b && teamId2 == a);
    }
}

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
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedLock _distributedLock;

    public ManualDrawCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
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
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Authorization
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
        if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل. قم بحذفها أولاً لإعادة التوليد.");

        var matches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();

        if (request.Request.GroupAssignments != null && request.Request.GroupAssignments.Any())
        {
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

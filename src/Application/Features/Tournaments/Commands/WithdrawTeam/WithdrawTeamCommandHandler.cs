using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.WithdrawTeam;

public class WithdrawTeamCommandHandler : IRequestHandler<WithdrawTeamCommand, Unit>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly IDistributedLock _distributedLock;

    public WithdrawTeamCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IAnalyticsService analyticsService,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _analyticsService = analyticsService;
        _distributedLock = distributedLock;
    }

    public async Task<Unit> Handle(WithdrawTeamCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-reg-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10)))
        {
            throw new ConflictException("يتم معالجة عملية أخرى على هذه البطولة حالياً. يرجى المحاولة لاحقاً.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        if (tournament.Status != TournamentStatus.RegistrationOpen && tournament.Status != TournamentStatus.RegistrationClosed)
        {
            throw new ConflictException("لا يمكن الانسحاب من البطولة بعد بدئها.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // Only captain can withdraw
        var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, cancellationToken);
        if (team == null || !team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain))
        {
            throw new ForbiddenException("فقط كابتن الفريق يمكنه سحب الفريق من البطولة.");
        }

        registration.Status = RegistrationStatus.Withdrawn;
        await _registrationRepository.UpdateAsync(registration, cancellationToken);

        // Cleanup participation
        var participations = await _tournamentPlayerRepository.FindAsync(tp => tp.RegistrationId == registration.Id, cancellationToken);
        await _tournamentPlayerRepository.DeleteRangeAsync(participations, cancellationToken);

        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        }

        await _analyticsService.LogActivityByTemplateAsync(
            "TEAM_WITHDRAWN", 
            new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, 
            request.UserId, 
            "كابتن الفريق",
            cancellationToken);

        return Unit.Value;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}

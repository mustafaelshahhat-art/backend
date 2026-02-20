using Application.Common.Interfaces;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.WithdrawTeam;

public class WithdrawTeamCommandHandler : IRequestHandler<WithdrawTeamCommand, Unit>
{
    private readonly ITournamentRegistrationContext _regContext;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IActivityLogger _activityLogger;

    public WithdrawTeamCommandHandler(
        ITournamentRegistrationContext regContext,
        IRepository<Team> teamRepository,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IActivityLogger activityLogger)
    {
        _regContext = regContext;
        _teamRepository = teamRepository;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _activityLogger = activityLogger;
    }

    public async Task<Unit> Handle(WithdrawTeamCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-reg-{request.TournamentId}";
        if (!await _regContext.DistributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10)))
        {
            throw new ConflictException("يتم معالجة عملية أخرى على هذه البطولة حالياً. يرجى المحاولة لاحقاً.");
        }

        try
        {
            var tournament = await _regContext.Tournaments.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        if (tournament.Status != TournamentStatus.RegistrationOpen && tournament.Status != TournamentStatus.RegistrationClosed)
        {
            throw new ConflictException("لا يمكن الانسحاب من البطولة بعد بدئها.");
        }

        var registration = (await _regContext.Registrations.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // Only captain can withdraw
        var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, cancellationToken);
        if (team == null || !team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain))
        {
            throw new ForbiddenException("فقط كابتن الفريق يمكنه سحب الفريق من البطولة.");
        }

        registration.Status = RegistrationStatus.Withdrawn;
        await _regContext.Registrations.UpdateAsync(registration, cancellationToken);

        // Cleanup participation
        var participations = await _tournamentPlayerRepository.FindAsync(tp => tp.RegistrationId == registration.Id, cancellationToken);
        await _tournamentPlayerRepository.DeleteRangeAsync(participations, cancellationToken);

        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            await _regContext.Tournaments.UpdateAsync(tournament, cancellationToken);
        }

await _activityLogger.LogAsync(
            "TEAM_WITHDRAWN", 
            new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, 
            request.UserId, 
            "كابتن الفريق",
            cancellationToken);

        return Unit.Value;
        }
        finally
        {
            await _regContext.DistributedLock.ReleaseLockAsync(lockKey);
        }
    }
}

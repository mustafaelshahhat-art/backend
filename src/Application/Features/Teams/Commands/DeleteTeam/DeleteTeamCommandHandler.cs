using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Commands.DeleteTeam;

public class DeleteTeamCommandHandler : IRequestHandler<DeleteTeamCommand, Unit>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly ITeamMemberDataService _memberData;
    private readonly ITournamentRegistrationContext _regContext;
    private readonly ITransactionManager _transactionManager;
    private readonly ITeamNotificationFacade _teamNotifier;

    public DeleteTeamCommandHandler(
        IRepository<Team> teamRepository, ITeamMemberDataService memberData,
        ITournamentRegistrationContext regContext, ITransactionManager transactionManager,
        ITeamNotificationFacade teamNotifier)
    {
        _teamRepository = teamRepository;
        _memberData = memberData;
        _regContext = regContext;
        _transactionManager = transactionManager;
        _teamNotifier = teamNotifier;
    }

    public async Task<Unit> Handle(DeleteTeamCommand request, CancellationToken ct)
    {
        await TeamAuthorizationHelper.ValidateManagementRights(_teamRepository, request.Id, request.UserId, request.UserRole, ct);

        var (memberUserIds, affectedTournamentIds) = await _transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var users = await _memberData.Users.FindAsync(u => u.TeamId == request.Id, ct);
            var memberIds = users.Select(u => u.Id).ToList();
            if (users.Any())
            {
                foreach (var user in users) user.TeamId = null;
                await _memberData.Users.UpdateRangeAsync(users, ct);
                foreach (var user in users)
                    await _teamNotifier.SendUserUpdatedAsync(user, ct);
            }

            var players = await _memberData.Players.FindAsync(p => p.TeamId == request.Id, ct);
            await _memberData.Players.DeleteRangeAsync(players, ct);

            var requests = await _memberData.JoinRequests.FindAsync(r => r.TeamId == request.Id, ct);
            await _memberData.JoinRequests.DeleteRangeAsync(requests, ct);

            var registrations = await _regContext.Registrations.FindAsync(r => r.TeamId == request.Id, ct);
            var tournamentIds = registrations.Select(r => r.TournamentId).Distinct().ToList();
            if (registrations.Any())
            {
                var approvedOrPending = registrations.Where(r => r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.PendingPaymentReview).ToList();
                if (approvedOrPending.Any())
                {
                    var tIds = approvedOrPending.Select(r => r.TournamentId).Distinct().ToList();
                    var tournaments = await _regContext.Tournaments.FindAsync(t => tIds.Contains(t.Id), ct);
                    foreach (var tournament in tournaments)
                    {
                        var affectedCount = approvedOrPending.Count(r => r.TournamentId == tournament.Id);
                        tournament.CurrentTeams = Math.Max(0, tournament.CurrentTeams - affectedCount);
                    }
                    await _regContext.Tournaments.UpdateRangeAsync(tournaments, ct);
                }
                await _regContext.Registrations.DeleteRangeAsync(registrations, ct);
            }

            await _teamRepository.DeleteAsync(request.Id, ct);
            return (memberIds, tournamentIds);
        }, ct);

        await _teamNotifier.SendTeamDeletedToMembersAsync(request.Id, memberUserIds, ct);
        await _teamNotifier.SendTeamDeletedAsync(request.Id, ct);

        foreach (var tournamentId in affectedTournamentIds)
        {
            var tournament = await _regContext.Tournaments.GetByIdAsync(tournamentId, ct);
            if (tournament != null)
                await _teamNotifier.SendTournamentUpdatedAsync(tournament, ct);
        }

        return Unit.Value;
    }
}

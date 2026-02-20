using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System.Linq.Expressions;

namespace Application.Features.Teams.Commands.RemovePlayer;

public class RemovePlayerCommandHandler : IRequestHandler<RemovePlayerCommand, Unit>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly ITeamMemberDataService _memberData;
    private readonly ITeamNotificationFacade _teamNotifier;

    public RemovePlayerCommandHandler(
        IRepository<Team> teamRepository, ITeamMemberDataService memberData,
        ITeamNotificationFacade teamNotifier)
    {
        _teamRepository = teamRepository;
        _memberData = memberData;
        _teamNotifier = teamNotifier;
    }

    public async Task<Unit> Handle(RemovePlayerCommand request, CancellationToken ct)
    {
        await TeamAuthorizationHelper.ValidateManagementRights(_teamRepository, request.TeamId, request.UserId, request.UserRole, ct);

        var player = await _memberData.Players.GetByIdAsync(request.PlayerId, ct);
        if (player != null && player.TeamId == request.TeamId)
        {
            var team = await _teamRepository.GetByIdAsync(request.TeamId,
                new Expression<Func<Team, object>>[] { t => t.Players }, ct);
            var captain = team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (team != null && player.UserId.HasValue && captain != null && player.UserId.Value == captain.UserId)
                throw new ForbiddenException("لا يمكن للكابتن حذف نفسه من الفريق. استخدم خيار حذف الفريق بدلاً من ذلك.");

            if (player.UserId.HasValue)
            {
                var targetUserId = player.UserId.Value;
                var user = await _memberData.Users.GetByIdAsync(targetUserId, ct);
                if (user != null)
                {
                    if (user.TeamId == request.TeamId)
                    {
                        var otherTeams = await _memberData.Players.FindAsync(p => p.UserId == targetUserId && p.TeamId != request.TeamId, ct);
                        user.TeamId = otherTeams.FirstOrDefault()?.TeamId;
                    }
                    await _memberData.Users.UpdateAsync(user, ct);
                    await _teamNotifier.SendUserUpdatedAsync(user, ct);
                    await _teamNotifier.SendRemovedFromTeamAsync(targetUserId, request.TeamId, request.PlayerId, ct);
                    await _teamNotifier.NotifyByTemplateAsync(targetUserId,
                        NotificationTemplates.PLAYER_REMOVED,
                        new Dictionary<string, string> { { "teamName", team?.Name ?? "الفريق" } },
                        entityId: request.TeamId, entityType: "team", ct: ct);
                }
            }

            await _memberData.Players.DeleteAsync(player, ct);

            await _teamNotifier.SendTeamUpdatedAsync(request.TeamId, ct);
        }

        return Unit.Value;
    }
}

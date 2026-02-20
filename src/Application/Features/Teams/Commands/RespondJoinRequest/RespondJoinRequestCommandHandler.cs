using Application.Common;
using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System.Linq.Expressions;

namespace Application.Features.Teams.Commands.RespondJoinRequest;

public class RespondJoinRequestCommandHandler : IRequestHandler<RespondJoinRequestCommand, JoinRequestDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly ITeamMemberDataService _memberData;
    private readonly ITeamNotificationFacade _teamNotifier;

    public RespondJoinRequestCommandHandler(
        IRepository<Team> teamRepository, ITeamMemberDataService memberData,
        ITeamNotificationFacade teamNotifier)
    {
        _teamRepository = teamRepository;
        _memberData = memberData;
        _teamNotifier = teamNotifier;
    }

    public async Task<JoinRequestDto> Handle(RespondJoinRequestCommand request, CancellationToken ct)
    {
        await TeamAuthorizationHelper.ValidateManagementRights(_teamRepository, request.TeamId, request.UserId, request.UserRole, ct);
        var joinReq = await _memberData.JoinRequests.GetByIdAsync(request.RequestId, ct);
        if (joinReq == null) throw new NotFoundException(nameof(TeamJoinRequest), request.RequestId);

        User? user = null;
        if (request.Approve)
        {
            joinReq.Status = "approved";
            user = await _memberData.Users.GetByIdAsync(joinReq.UserId, ct);
            if (user != null)
            {
                if (!user.TeamId.HasValue) user.TeamId = request.TeamId;
                await _memberData.Users.UpdateAsync(user, ct);
                await _teamNotifier.SendUserUpdatedAsync(user, ct);

                var player = new Player
                {
                    Name = user.Name, DisplayId = "P-" + user.DisplayId,
                    UserId = user.Id, TeamId = request.TeamId, TeamRole = TeamRole.Member
                };
                await _memberData.Players.AddAsync(player, ct);
            }
        }
        else
        {
            joinReq.Status = "rejected";
        }

        await _memberData.JoinRequests.UpdateAsync(joinReq, ct);

        if (request.Approve)
            await _teamNotifier.SendTeamUpdatedAsync(request.TeamId, ct);

        var team = await _teamRepository.GetByIdAsync(request.TeamId,
            new Expression<Func<Team, object>>[] { t => t.Players }, ct);
        user = await _memberData.Users.GetByIdAsync(joinReq.UserId, ct);

        await _teamNotifier.NotifyByTemplateAsync(joinReq.UserId,
            request.Approve ? NotificationTemplates.PLAYER_JOINED_TEAM : NotificationTemplates.JOIN_REQUEST_REJECTED,
            new Dictionary<string, string> { { "teamName", team?.Name ?? "الفريق" } },
            entityId: request.TeamId, entityType: "team", ct: ct);

        return new JoinRequestDto
        {
            Id = joinReq.Id, PlayerId = joinReq.UserId,
            PlayerName = user?.Name ?? "Unknown", TeamId = request.TeamId,
            TeamName = team?.Name ?? "Unknown", Status = joinReq.Status,
            RequestDate = joinReq.CreatedAt, InitiatedByPlayer = joinReq.InitiatedByPlayer
        };
    }
}

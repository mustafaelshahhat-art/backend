using Application.Common;
using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.TeamRequests.Commands.AcceptInvite;

public class AcceptInviteCommandHandler : IRequestHandler<AcceptInviteCommand, JoinRequestDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly ITeamMemberDataService _memberData;
    private readonly ITeamNotificationFacade _teamNotifier;

    public AcceptInviteCommandHandler(
        IRepository<Team> teamRepository,
        ITeamMemberDataService memberData,
        ITeamNotificationFacade teamNotifier)
    {
        _teamRepository = teamRepository;
        _memberData = memberData;
        _teamNotifier = teamNotifier;
    }

    public async Task<JoinRequestDto> Handle(AcceptInviteCommand request, CancellationToken ct)
    {
        var joinReq = await _memberData.JoinRequests.GetByIdAsync(request.RequestId, new[] { "Team.Players" }, ct);
        if (joinReq == null) throw new NotFoundException(nameof(TeamJoinRequest), request.RequestId);
        if (joinReq.UserId != request.UserId) throw new ForbiddenException("لا تملك صلاحية قبول هذه الدعوة.");
        if (joinReq.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        var user = await _memberData.Users.GetByIdAsync(request.UserId, ct);
        if (user == null) throw new NotFoundException(nameof(User), request.UserId);

        joinReq.Status = "approved";
        await _memberData.JoinRequests.UpdateAsync(joinReq, ct);

        if (!user.TeamId.HasValue) user.TeamId = joinReq.TeamId;
        await _memberData.Users.UpdateAsync(user, ct);
        await _teamNotifier.SendUserUpdatedAsync(user, ct);

        var player = new Player
        {
            Name = user.Name, DisplayId = "P-" + user.DisplayId,
            UserId = user.Id, TeamId = joinReq.TeamId, TeamRole = TeamRole.Member
        };
        await _memberData.Players.AddAsync(player, ct);

        // Broadcast team snapshot
        await _teamNotifier.SendTeamUpdatedAsync(joinReq.TeamId, ct);

        // Notify captain
        if (joinReq.Team != null)
        {
            var captain = joinReq.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain?.UserId.HasValue == true)
            {
                await _teamNotifier.NotifyByTemplateAsync(captain.UserId.Value,
                    NotificationTemplates.INVITE_ACCEPTED,
                    new Dictionary<string, string> { { "playerName", user.Name } }, ct: ct);
            }
        }

        return new JoinRequestDto
        {
            Id = joinReq.Id, PlayerId = request.UserId,
            PlayerName = user.Name, TeamId = joinReq.TeamId,
            TeamName = joinReq.Team?.Name ?? "Unknown", Status = "approved",
            RequestDate = joinReq.CreatedAt
        };
    }
}

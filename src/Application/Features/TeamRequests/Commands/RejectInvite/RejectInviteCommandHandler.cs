using Application.Common;
using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.TeamRequests.Commands.RejectInvite;

public class RejectInviteCommandHandler : IRequestHandler<RejectInviteCommand, JoinRequestDto>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly INotificationService _notificationService;

    public RejectInviteCommandHandler(
        IRepository<User> userRepository,
        IRepository<TeamJoinRequest> joinRequestRepository,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _joinRequestRepository = joinRequestRepository;
        _notificationService = notificationService;
    }

    public async Task<JoinRequestDto> Handle(RejectInviteCommand request, CancellationToken ct)
    {
        var joinReq = await _joinRequestRepository.GetByIdAsync(request.RequestId, new[] { "Team.Players" }, ct);
        if (joinReq == null) throw new NotFoundException(nameof(TeamJoinRequest), request.RequestId);
        if (joinReq.UserId != request.UserId) throw new ForbiddenException("لا تملك صلاحية رفض هذه الدعوة.");
        if (joinReq.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        joinReq.Status = "rejected";
        await _joinRequestRepository.UpdateAsync(joinReq, ct);

        if (joinReq.Team != null)
        {
            var captain = joinReq.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain?.UserId.HasValue == true)
            {
                var user = await _userRepository.GetByIdAsync(request.UserId, ct);
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value,
                    NotificationTemplates.INVITE_REJECTED,
                    new Dictionary<string, string> { { "playerName", user?.Name ?? "اللاعب" } }, ct: ct);
            }
        }

        return new JoinRequestDto
        {
            Id = joinReq.Id, PlayerId = request.UserId,
            Status = "rejected", RequestDate = joinReq.CreatedAt
        };
    }
}

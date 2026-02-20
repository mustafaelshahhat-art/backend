using Application.Common;
using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System.Linq.Expressions;

namespace Application.Features.Teams.Commands.InvitePlayer;

public class InvitePlayerCommandHandler : IRequestHandler<InvitePlayerCommand, JoinRequestDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly INotificationService _notificationService;

    public InvitePlayerCommandHandler(
        IRepository<Team> teamRepository, IRepository<User> userRepository,
        IRepository<TeamJoinRequest> joinRequestRepository, INotificationService notificationService)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _joinRequestRepository = joinRequestRepository;
        _notificationService = notificationService;
    }

    public async Task<JoinRequestDto> Handle(InvitePlayerCommand request, CancellationToken ct)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId,
            new Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain == null || captain.UserId != request.CaptainId)
            throw new ForbiddenException("فقط قائد الفريق يمكنه إرسال دعوات.");

        var users = await _userRepository.FindAsync(u => u.DisplayId == request.Request.DisplayId, ct);
        var user = users.FirstOrDefault();
        if (user == null) throw new NotFoundException("لم يتم العثور على لاعب بهذا الرقم التعريفي.");

        if (user.TeamId == request.TeamId || team.Players.Any(p => p.UserId == user.Id))
            throw new ConflictException("اللاعب مسجل بالفعل في هذا الفريق.");

        var existingRequest = await _joinRequestRepository.FindAsync(
            r => r.TeamId == request.TeamId && r.UserId == user.Id && r.Status == "pending", ct);
        if (existingRequest.Any()) throw new ConflictException("تم إرسال دعوة بالفعل لهذا اللاعب.");

        var joinRequest = new TeamJoinRequest
        {
            TeamId = request.TeamId, UserId = user.Id,
            Status = "pending", InitiatedByPlayer = false
        };
        await _joinRequestRepository.AddAsync(joinRequest, ct);

        await _notificationService.SendNotificationByTemplateAsync(user.Id,
            NotificationTemplates.INVITE_RECEIVED,
            new Dictionary<string, string> { { "teamName", team.Name } },
            entityId: team.Id, entityType: "team", ct: ct);

        return new JoinRequestDto
        {
            Id = joinRequest.Id, PlayerId = user.Id, PlayerName = user.Name,
            Status = "pending", RequestDate = joinRequest.CreatedAt, InitiatedByPlayer = false
        };
    }
}

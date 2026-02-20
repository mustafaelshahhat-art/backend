using Application.Common;
using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;
using System.Linq.Expressions;

namespace Application.Features.Teams.Commands.RequestJoinTeam;

public class RequestJoinTeamCommandHandler : IRequestHandler<RequestJoinTeamCommand, JoinRequestDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly INotificationService _notificationService;

    public RequestJoinTeamCommandHandler(
        IRepository<Team> teamRepository, IRepository<User> userRepository,
        IRepository<TeamJoinRequest> joinRequestRepository, INotificationService notificationService)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _joinRequestRepository = joinRequestRepository;
        _notificationService = notificationService;
    }

    public async Task<JoinRequestDto> Handle(RequestJoinTeamCommand request, CancellationToken ct)
    {
        var existingRequest = await _joinRequestRepository.FindAsync(
            r => r.TeamId == request.TeamId && r.UserId == request.PlayerId && r.Status == "pending", ct);
        if (existingRequest.Any())
        {
            return new JoinRequestDto
            {
                Id = existingRequest.First().Id, PlayerId = request.PlayerId,
                Status = "pending", RequestDate = existingRequest.First().CreatedAt
            };
        }

        var user = await _userRepository.GetByIdAsync(request.PlayerId, ct);
        if (user == null) throw new NotFoundException(nameof(User), request.PlayerId);

        var joinRequest = new TeamJoinRequest
        {
            TeamId = request.TeamId, UserId = request.PlayerId,
            Status = "pending", InitiatedByPlayer = true
        };
        await _joinRequestRepository.AddAsync(joinRequest, ct);

        var team = await _teamRepository.GetByIdAsync(request.TeamId,
            new Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team != null)
        {
            var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain?.UserId.HasValue == true)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value,
                    NotificationTemplates.JOIN_REQUEST_RECEIVED,
                    new Dictionary<string, string> { { "playerName", user.Name } },
                    entityId: request.TeamId, entityType: "team", ct: ct);
            }
        }

        return new JoinRequestDto
        {
            Id = joinRequest.Id, PlayerId = request.PlayerId,
            PlayerName = user.Name, TeamId = request.TeamId,
            TeamName = team?.Name ?? "Unknown", Status = "pending",
            RequestDate = joinRequest.CreatedAt, InitiatedByPlayer = true
        };
    }
}

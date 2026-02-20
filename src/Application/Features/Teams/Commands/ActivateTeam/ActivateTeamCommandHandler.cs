using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Teams.Commands.ActivateTeam;

public class ActivateTeamCommandHandler : IRequestHandler<ActivateTeamCommand, Unit>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly INotificationService _notificationService;

    public ActivateTeamCommandHandler(IRepository<Team> teamRepository, INotificationService notificationService)
    {
        _teamRepository = teamRepository;
        _notificationService = notificationService;
    }

    public async Task<Unit> Handle(ActivateTeamCommand request, CancellationToken ct)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, ct);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

        team.IsActive = true;
        await _teamRepository.UpdateAsync(team, ct);

        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain?.UserId.HasValue == true)
        {
            await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value,
                NotificationTemplates.TEAM_ACTIVATED, entityId: request.TeamId, entityType: "team", ct: ct);
        }

        return Unit.Value;
    }
}

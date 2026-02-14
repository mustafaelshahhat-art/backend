using Application.Interfaces;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Tournaments.EventHandlers;

public class TournamentRegistrationRejectedEventHandler : INotificationHandler<TournamentRegistrationRejectedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<TournamentRegistrationRejectedEventHandler> _logger;

    public TournamentRegistrationRejectedEventHandler(
        INotificationService notificationService,
        ILogger<TournamentRegistrationRejectedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(TournamentRegistrationRejectedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling TournamentRegistrationRejectedEvent for Team: {TeamName} in Tournament: {TournamentName}", 
            notification.TeamName, notification.TournamentName);

        await _notificationService.SendNotificationByTemplateAsync(
            notification.CaptainUserId,
            "TEAM_REJECTED",
            new Dictionary<string, string> {
                { "teamName", notification.TeamName },
                { "tournamentName", notification.TournamentName },
                { "reason", notification.Reason }
            },
            "registration_rejected"
        );
    }
}

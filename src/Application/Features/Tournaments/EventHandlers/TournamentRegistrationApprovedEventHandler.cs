using Application.Interfaces;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Tournaments.EventHandlers;

public class TournamentRegistrationApprovedEventHandler : INotificationHandler<TournamentRegistrationApprovedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _notifier;
    private readonly ILogger<TournamentRegistrationApprovedEventHandler> _logger;
    private readonly Domain.Interfaces.IRepository<Domain.Entities.Tournament> _tournamentRepository;
    private readonly AutoMapper.IMapper _mapper;

    public TournamentRegistrationApprovedEventHandler(
        INotificationService notificationService,
        IRealTimeNotifier notifier,
        ILogger<TournamentRegistrationApprovedEventHandler> logger,
        Domain.Interfaces.IRepository<Domain.Entities.Tournament> tournamentRepository,
        AutoMapper.IMapper mapper)
    {
        _notificationService = notificationService;
        _notifier = notifier;
        _logger = logger;
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
    }

    public async Task Handle(TournamentRegistrationApprovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling TournamentRegistrationApprovedEvent for Team: {TeamName} in Tournament: {TournamentName}", 
            notification.TeamName, notification.TournamentName);

        // 1. Send Background Notification
        await _notificationService.SendNotificationByTemplateAsync(
            notification.CaptainUserId,
            "TEAM_APPROVED",
            new Dictionary<string, string> {
                { "teamName", notification.TeamName },
                { "tournamentName", notification.TournamentName }
            },
            "registration_approved"
        );

        // 2. Notify Real-Time (Best effort, since it's now in the outbox processor)
        var tournament = await _tournamentRepository.GetByIdNoTrackingAsync(notification.TournamentId, new[] { "Registrations", "WinnerTeam" });
        if (tournament != null)
        {
            var dto = _mapper.Map<Application.DTOs.Tournaments.TournamentDto>(tournament);
            await _notifier.SendTournamentUpdatedAsync(dto);
        }
    }
}

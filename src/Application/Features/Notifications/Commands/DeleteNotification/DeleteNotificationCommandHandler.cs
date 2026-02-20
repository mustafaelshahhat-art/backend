using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Commands.DeleteNotification;

public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand, Unit>
{
    private readonly INotificationService _service;

    public DeleteNotificationCommandHandler(INotificationService service) => _service = service;

    public async Task<Unit> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(request.NotificationId, request.UserId, cancellationToken);
        return Unit.Value;
    }
}

using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Commands.MarkAsRead;

public class MarkAsReadCommandHandler : IRequestHandler<MarkAsReadCommand, Unit>
{
    private readonly INotificationService _service;

    public MarkAsReadCommandHandler(INotificationService service) => _service = service;

    public async Task<Unit> Handle(MarkAsReadCommand request, CancellationToken cancellationToken)
    {
        await _service.MarkAsReadAsync(request.NotificationId, request.UserId, cancellationToken);
        return Unit.Value;
    }
}

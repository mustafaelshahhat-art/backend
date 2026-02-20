using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Commands.MarkAllAsRead;

public class MarkAllAsReadCommandHandler : IRequestHandler<MarkAllAsReadCommand, Unit>
{
    private readonly INotificationService _service;

    public MarkAllAsReadCommandHandler(INotificationService service) => _service = service;

    public async Task<Unit> Handle(MarkAllAsReadCommand request, CancellationToken cancellationToken)
    {
        await _service.MarkAllAsReadAsync(request.UserId, cancellationToken);
        return Unit.Value;
    }
}

using MediatR;

namespace Application.Features.Notifications.Commands.MarkAsRead;

public record MarkAsReadCommand(Guid NotificationId, Guid UserId) : IRequest<Unit>;

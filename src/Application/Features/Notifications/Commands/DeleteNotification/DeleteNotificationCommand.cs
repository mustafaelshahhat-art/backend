using MediatR;

namespace Application.Features.Notifications.Commands.DeleteNotification;

public record DeleteNotificationCommand(Guid NotificationId, Guid UserId) : IRequest<Unit>;

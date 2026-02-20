using MediatR;

namespace Application.Features.Notifications.Commands.MarkAllAsRead;

public record MarkAllAsReadCommand(Guid UserId) : IRequest<Unit>;

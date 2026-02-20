using Application.Common.Models;
using Application.DTOs.Notifications;
using MediatR;

namespace Application.Features.Notifications.Queries.GetUserNotifications;

public record GetUserNotificationsQuery(Guid UserId, int Page, int PageSize) : IRequest<PagedResult<NotificationDto>>;

using Application.Common.Models;
using Application.DTOs.Notifications;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Queries.GetUserNotifications;

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly INotificationService _service;

    public GetUserNotificationsQueryHandler(INotificationService service) => _service = service;

    public Task<PagedResult<NotificationDto>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
        => _service.GetUserNotificationsAsync(request.UserId, request.Page, request.PageSize, null, null, cancellationToken);
}

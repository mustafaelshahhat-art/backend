using Application.Interfaces;
using MediatR;

namespace Application.Features.Notifications.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationService _service;

    public GetUnreadCountQueryHandler(INotificationService service) => _service = service;

    public Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
        => _service.GetUnreadCountAsync(request.UserId, cancellationToken);
}

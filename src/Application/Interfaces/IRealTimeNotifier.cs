using System;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IRealTimeNotifier
{
    Task SafeSendNotificationAsync(Guid userId, Notification notification);
    Task SendAccountStatusChangedAsync(Guid userId, string newStatus);
    Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId);
    Task SendTeamDeletedAsync(Guid teamId, System.Collections.Generic.IEnumerable<Guid> userIds);
}

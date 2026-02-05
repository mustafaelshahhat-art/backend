using System;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IRealTimeNotifier
{
    Task SafeSendNotificationAsync(Guid userId, Notification notification);
}

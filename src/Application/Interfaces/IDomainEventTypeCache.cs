using System;

namespace Application.Interfaces;

public interface IDomainEventTypeCache
{
    Type? GetEventType(string typeName);
}

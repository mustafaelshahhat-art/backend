using MediatR;

namespace Domain.Interfaces;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}

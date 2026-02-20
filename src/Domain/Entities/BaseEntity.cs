using System;

namespace Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private readonly List<Domain.Interfaces.IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<Domain.Interfaces.IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(Domain.Interfaces.IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

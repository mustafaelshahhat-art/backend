using Application.Common.Interfaces;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

/// <summary>
/// Unit of Work implementation wrapping DbContext.SaveChangesAsync()
/// and domain event dispatch via MediatR.
///
/// Collects domain events from tracked entities BEFORE saving,
/// clears them to prevent re-dispatch, saves atomically, then
/// publishes events AFTER successful commit.
///
/// Replaces scattered repository.UpdateAsync() calls that each
/// trigger SaveChanges independently. Handlers should use
/// _unitOfWork.SaveChangesAsync() for a single atomic save.
///
/// See: EXECUTION_PLAN §3.6, EXECUTION_BLUEPRINT §3.1 row 3
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IMediator _mediator;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(
        AppDbContext context,
        IMediator mediator,
        ILogger<UnitOfWork> logger)
    {
        _context = context;
        _mediator = mediator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // 1. Collect domain events from tracked entities BEFORE save
        var domainEvents = _context.ChangeTracker
            .Entries<Domain.Entities.BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // 2. Clear events from entities (prevent re-dispatch)
        foreach (var entry in _context.ChangeTracker.Entries<Domain.Entities.BaseEntity>())
            entry.Entity.ClearDomainEvents();

        // 3. Save all changes atomically
        var result = await _context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "UnitOfWork saved {ChangeCount} changes, dispatching {EventCount} domain events",
            result, domainEvents.Count);

        // 4. Dispatch domain events AFTER successful save
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogDebug(
                "Dispatching domain event {EventType} occurred at {OccurredOn}",
                domainEvent.GetType().Name, domainEvent.OccurredOn);

            await _mediator.Publish(domainEvent, ct);
        }

        return result;
    }
}

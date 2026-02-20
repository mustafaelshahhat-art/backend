using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that acquires a distributed lock BEFORE the handler runs.
/// Only activates for commands that implement IRequiresLock.
///
/// Pipeline position: AFTER PerformanceBehavior, BEFORE TransactionBehavior.
/// This ensures the lock is held for the entire transaction scope.
///
/// Replaces inline distributed lock patterns scattered throughout
/// TournamentService and command handlers (RegisterTeam, GenerateMatches, etc.).
/// </summary>
public class DistributedLockBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IDistributedLock _lockService;
    private readonly ILogger<DistributedLockBehavior<TRequest, TResponse>> _logger;

    public DistributedLockBehavior(
        IDistributedLock lockService,
        ILogger<DistributedLockBehavior<TRequest, TResponse>> logger)
    {
        _lockService = lockService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Only lock if the request requires it
        if (request is not IRequiresLock lockRequest)
            return await next();

        var lockKey = lockRequest.LockKey;
        var lockTimeout = lockRequest.LockTimeout ?? TimeSpan.FromSeconds(30);

        _logger.LogDebug("Acquiring distributed lock: {LockKey}", lockKey);

        var acquired = await _lockService.AcquireLockAsync(lockKey, lockTimeout, ct);
        if (!acquired)
        {
            _logger.LogWarning("Failed to acquire distributed lock: {LockKey}", lockKey);
            throw new Shared.Exceptions.ConflictException(
                "العملية قيد التنفيذ حالياً. يرجى المحاولة لاحقاً.");
        }

        try
        {
            return await next();
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockKey, ct);
            _logger.LogDebug("Released distributed lock: {LockKey}", lockKey);
        }
    }
}

/// <summary>
/// Marker interface for commands that require distributed locking.
/// Implement on command records that mutate shared state.
///
/// Usage:
/// <code>
/// public record RegisterTeamCommand(...) : IRequest&lt;Result&gt;, IRequiresLock
/// {
///     public string LockKey => $"tournament:{TournamentId}:register";
///     public TimeSpan? LockTimeout => TimeSpan.FromSeconds(15);
/// }
/// </code>
/// </summary>
public interface IRequiresLock
{
    /// <summary>The unique lock key (e.g., "tournament:{id}:register")</summary>
    string LockKey { get; }

    /// <summary>Optional timeout override. Default: 30s.</summary>
    TimeSpan? LockTimeout => null;
}

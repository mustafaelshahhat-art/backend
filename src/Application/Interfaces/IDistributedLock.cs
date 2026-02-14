using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IDistributedLock
{
    /// <summary>
    /// Attempts to acquire a distributed lock.
    /// </summary>
    /// <param name="key">The unique key for the lock.</param>
    /// <param name="expiry">The TTL for the lock.</param>
    /// <returns>True if the lock was acquired, false otherwise.</returns>
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// Releases the distributed lock.
    /// </summary>
    /// <param name="key">The unique key for the lock.</param>
    Task ReleaseLockAsync(string key, CancellationToken ct = default);
}

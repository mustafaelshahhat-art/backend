using Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services;

/// <summary>
/// PROD-AUDIT: Fail-safe SQL Server distributed lock using sp_getapplock.
/// This is used as a fallback if Redis is unavailable or unconfigured.
/// </summary>
public class SqlDistributedLockService : IDistributedLock
{
    private readonly string _connectionString;
    private readonly ILogger<SqlDistributedLockService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SqlConnection> _activeConnections = new();

    public SqlDistributedLockService(IConfiguration configuration, ILogger<SqlDistributedLockService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string is missing.");
        _logger = logger;
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var command = connection.CreateCommand();
            command.CommandText = "sp_getapplock";
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.Add(new SqlParameter("@Resource", key));
            command.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
            command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
            command.Parameters.Add(new SqlParameter("@LockTimeout", 0)); // Fail immediately if busy

            var resultParam = new SqlParameter("@ReturnVal", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(resultParam);

            await command.ExecuteNonQueryAsync(ct);
            var result = (int)resultParam.Value;

            var success = result >= 0;

            if (success)
            {
                if (!_activeConnections.TryAdd(key, connection))
                {
                    // This shouldn't happen if lock is exclusive and Release is called correctly, 
                    // but for safety:
                    await connection.DisposeAsync();
                    return false;
                }
                _logger.LogInformation("Successfully acquired SQL distributed lock for key: {Key}", key);
            }
            else
            {
                await connection.DisposeAsync();
                _logger.LogDebug("Failed to acquire SQL distributed lock for key: {Key}. Result: {Result}", key, result);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL error while acquiring distributed lock for key: {Key}. Failing safe – lock NOT acquired.", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key, CancellationToken ct = default)
    {
        if (_activeConnections.TryRemove(key, out var connection))
        {
            try
            {
                using (connection)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "sp_releaseapplock";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@Resource", key));
                    command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));

                    await command.ExecuteNonQueryAsync(ct);
                    _logger.LogInformation("Released SQL distributed lock for key: {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing SQL distributed lock for key: {Key}", key);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to release SQL distributed lock for key: {Key}, but no active connection was found.", key);
        }
    }
}

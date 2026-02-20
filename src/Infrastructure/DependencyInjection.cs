using Application.Common.Interfaces;
using Application.Interfaces;
using Domain.Interfaces;
using Infrastructure.Authentication;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["Redis:ConnectionString"] 
            ?? configuration.GetConnectionString("Redis") 
            ?? "localhost:6379";

        bool useRedis = false;

        // Skip Redis probe entirely if connection string is empty
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                // PROD-AUDIT: Distributed Safety
                // Attempt Redis connection, fallback to SQL if unavailable (even in Production)
                var checkOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
                checkOptions.AbortOnConnectFail = true;
                checkOptions.ConnectTimeout = 3000; // Fast fail check
                using var checkConn = StackExchange.Redis.ConnectionMultiplexer.Connect(checkOptions);
                useRedis = checkConn.IsConnected;
            }
            catch (Exception ex)
            {
                useRedis = false;
                // PROD-FIX: Log the fallback so operators know Redis is down
                // Note: Serilog may not be fully configured yet, use Console as fallback
                Console.Error.WriteLine($"[WARN] Redis unavailable at '{redisConnectionString}': {ex.Message}. Falling back to SQL distributed lock and in-memory cache.");
            }
        }

        if (useRedis)
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
            {
                var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false; 
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
            });

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "KoraZone365_";
            });
        }
        else
        {
            // ENTERPRISE: In non-Redis environments, still use IDistributedCache interface
            // but log a warning — production MUST use Redis for horizontal scaling.
            Console.Error.WriteLine("[WARN] Redis unavailable. Using in-memory distributed cache. NOT safe for horizontal scaling.");
            services.AddDistributedMemoryCache();
        }

        // Lock Provider Logic
        var lockProvider = configuration["DistributedLock:Provider"];
        bool useRedisLock = false;

        if (string.IsNullOrEmpty(lockProvider))
        {
             useRedisLock = useRedis; // Default to Redis if available, else Sql
        }
        else
        {
             useRedisLock = lockProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase);
        }

        if (useRedisLock && useRedis)
        {
            services.AddSingleton<IDistributedLock, Services.RedisLockService>();
        }
        else
        {
            services.AddSingleton<IDistributedLock, Services.SqlDistributedLockService>();
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    // PERF: 3 retries × 5s vs old 5 retries × 30s.
                    // On shared/throttled SQL, long retries hold the connection open,
                    // exhaust the pool faster, and cause 150s request hangs.
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                    // PERF: 15s command timeout (was 30s EF default).
                    // Shared SQL I/O throttling makes long-running queries dangerous;
                    // fail fast and let the client retry rather than hold a pool connection.
                    sqlOptions.CommandTimeout(15);
                }));

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITransactionManager, TransactionManager>();
        
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMatchMessageRepository, MatchMessageRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IUserCacheService, Services.UserCacheService>();
        services.AddScoped<IFileStorageService, Services.CloudFileStorageService>();
        services.Configure<Services.FileStorageOptions>(configuration.GetSection(Services.FileStorageOptions.SectionName));
        
        services.AddSingleton<Infrastructure.Logging.BackgroundActivityLogger>();
        services.AddSingleton<IBackgroundActivityLogger>(sp => sp.GetRequiredService<Infrastructure.Logging.BackgroundActivityLogger>());
        services.AddHostedService(sp => sp.GetRequiredService<Infrastructure.Logging.BackgroundActivityLogger>());

        services.AddScoped<Services.EmailService>();
        services.AddScoped<IEmailService, Services.ResilientEmailService>(sp => 
            new Services.ResilientEmailService(
                sp.GetRequiredService<Services.EmailService>(), 
                sp.GetRequiredService<ILogger<Services.ResilientEmailService>>()));
        services.AddScoped<IOutboxAdminService, Services.OutboxAdminService>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.TeamCaptainHandler>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.TournamentOwnerHandler>();

        // Phase 1+2: New cross-cutting abstractions (EXECUTION_PLAN §2.1, §2.2)
        services.AddScoped<IAuthorizationChecker, Authorization.AuthorizationChecker>();
        services.AddScoped<INotificationDispatcher, Services.NotificationDispatcher>();
        services.AddScoped<IActivityLogger, Logging.ActivityLogger>();
        services.AddScoped<IUnitOfWork, Data.UnitOfWork>();

        services.AddSingleton<IDomainEventTypeCache, Infrastructure.BackgroundJobs.DomainEventTypeCache>();
        services.AddHostedService<Infrastructure.BackgroundJobs.TournamentBackgroundService>();
        services.AddHostedService<Infrastructure.BackgroundJobs.OutboxProcessor>();
        services.AddHostedService<Infrastructure.BackgroundJobs.IdempotencyCleanupService>();

        // PERF-FIX B4: Channel-based email queue replaces Task.Run fire-and-forget
        services.AddSingleton<Infrastructure.BackgroundJobs.EmailQueueService>();
        services.AddSingleton<IEmailQueueService>(sp => sp.GetRequiredService<Infrastructure.BackgroundJobs.EmailQueueService>());
        services.AddHostedService(sp => sp.GetRequiredService<Infrastructure.BackgroundJobs.EmailQueueService>());

        return services;
    }
}

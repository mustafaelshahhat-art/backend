using Application.Common.Interfaces;
using Application.Interfaces;
using Domain.Interfaces;
using Infrastructure.Authentication;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Single source of truth: ConnectionStrings:Redis (env var: ConnectionStrings__Redis)
        var redisConnectionString = configuration.GetConnectionString("Redis");
        bool redisConfigured = !string.IsNullOrWhiteSpace(redisConnectionString);
        bool useRedis = false;

        if (redisConfigured)
        {
            try
            {
                var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString!);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ConnectTimeout = 5000;
                var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(options);
                useRedis = multiplexer.IsConnected;

                if (useRedis)
                {
                    // Register the already-connected multiplexer as singleton (no duplicate probe)
                    services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(multiplexer);
                    services.AddStackExchangeRedisCache(opt =>
                    {
                        opt.Configuration = redisConnectionString;
                        opt.InstanceName = "KoraZone365_";
                    });
                    Console.WriteLine($"[CONFIG] Cache provider: Redis ({redisConnectionString})");
                }
                else
                {
                    multiplexer.Dispose();
                    throw new InvalidOperationException($"Redis connected but IsConnected=false for '{redisConnectionString}'.");
                }
            }
            catch (Exception ex)
            {
                useRedis = false;

                if (environment.IsProduction())
                {
                    // Production: fail fast — do not silently degrade to in-memory
                    throw new InvalidOperationException(
                        $"CRITICAL: Redis is configured (ConnectionStrings:Redis) but unreachable. " +
                        $"Cannot start in Production without Redis. Error: {ex.Message}", ex);
                }

                // Development: allow fallback for convenience
                Console.WriteLine($"[CONFIG] Redis configured but unreachable ({ex.Message}). Falling back to in-memory cache (Development only).");
            }
        }

        if (!useRedis)
        {
            services.AddDistributedMemoryCache();
            Console.WriteLine("[CONFIG] Cache provider: In-Memory (DistributedMemoryCache)");
        }

        // Distributed Lock: follows Redis availability — no separate config key needed
        if (useRedis)
        {
            services.AddSingleton<IDistributedLock, Services.RedisLockService>();
            Console.WriteLine("[CONFIG] Distributed lock provider: Redis");
        }
        else
        {
            services.AddSingleton<IDistributedLock, Services.SqlDistributedLockService>();
            Console.WriteLine("[CONFIG] Distributed lock provider: SQL (sp_getapplock)");
        }

        // PERF-FIX: Use AddDbContextPool for context reuse — reduces GC pressure
        // and eliminates per-request model creation overhead.
        // The NoTracking default is set in OnConfiguring (required for pooling).
        services.AddDbContextPool<AppDbContext>(options =>
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

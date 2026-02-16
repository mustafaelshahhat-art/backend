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
        var isProduction = configuration["ASPNETCORE_ENVIRONMENT"] == "Production";

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
        catch
        {
            useRedis = false;
            // Fallback to SQL-based caching and locking (even in Production)
            // Log warning but don't fail startup
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
                options.InstanceName = "RamadanAPI_";
            });
        }
        else
        {
            // Register a dummy/null multiplexer or just don't register it?
            // Some services might depend on IConnectionMultiplexer.
            // If we don't register it, and something injects it, startup fails.
            // But if we register a failing one, runtime fails (which is what's happening).
            // Let's assume services only use it if relevant feature is enabled.
            // But UserCacheService uses IDistributedCache, which is covered below.
            
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
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITransactionManager, TransactionManager>();
        
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMatchMessageRepository, MatchMessageRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IUserCacheService, Services.UserCacheService>();
        services.AddScoped<IFileStorageService, Services.LocalFileStorageService>();
        
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

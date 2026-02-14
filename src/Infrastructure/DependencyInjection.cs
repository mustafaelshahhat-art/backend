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
        // PROD-AUDIT: Distributed Safety (Redis)
        var redisConnectionString = configuration["Redis:ConnectionString"] 
            ?? configuration.GetConnectionString("Redis") 
            ?? "localhost:6379";

        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
        {
            var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false; 
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
        });
            
        // PROD-AUDIT: Distributed Safety
        // Use SQL Fallback in Development for convenience, enforce Redis in Production for performance.
        var isProduction = configuration["ASPNETCORE_ENVIRONMENT"] == "Production";
        var lockProvider = configuration["DistributedLock:Provider"];
        
        if (string.IsNullOrEmpty(lockProvider))
        {
            lockProvider = isProduction ? "Redis" : "Sql";
        }

        if (lockProvider.Equals("Sql", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IDistributedLock, Services.SqlDistributedLockService>();
        }
        else
        {
            services.AddSingleton<IDistributedLock, Services.RedisLockService>();
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

        return services;
    }
}

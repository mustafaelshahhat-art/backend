using System.Reflection;
using Application.Interfaces;
using Application.Mapping;
using Application.Services;
using Application.Validators;
using FluentValidation;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
        services.AddValidatorsFromAssembly(typeof(RegisterRequestValidator).Assembly);
        
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.LoggingBehavior<,>)); // If I changed it to IPipelineBehavior
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.PerformanceBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.DistributedLockBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.TransactionBehavior<,>));
        });

        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITournamentLifecycleService, TournamentLifecycleService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IActivityLogMigrationService, ActivityLogMigrationService>();

        // Composite facades to reduce handler constructor dependencies
        services.AddScoped<IAuthUserResolverService, AuthUserResolverService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IMatchEventNotifier, MatchEventNotifierService>();
        services.AddScoped<ITeamNotificationFacade, TeamNotificationFacadeService>();
        services.AddScoped<ITournamentRegistrationContext, TournamentRegistrationContext>();
        services.AddScoped<ITeamMemberDataService, TeamMemberDataService>();

        return services;
    }
}

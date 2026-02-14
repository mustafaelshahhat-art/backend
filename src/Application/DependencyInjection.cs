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
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.TransactionBehavior<,>));
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ITournamentLifecycleService, TournamentLifecycleService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ActivityLogMigrationService>();

        return services;
    }
}

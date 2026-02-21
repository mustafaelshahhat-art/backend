using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Domain.Enums;
using Domain.Entities;
using Domain.Interfaces;
using Serilog;

namespace Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    // PROD-AUDIT: Global Request Size Limit + PERF-FIX I2: Kestrel thread tuning
    // PERF: Tuned for 256MB shared hosting — lower connection caps to reduce memory pressure
    public static WebApplicationBuilder ConfigureKestrel(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            serverOptions.Limits.MaxConcurrentConnections = 100;
            serverOptions.Limits.MaxConcurrentUpgradedConnections = 50; // WebSockets/SignalR
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        return builder;
    }

    // PERF-FIX I4: Output Caching for read-heavy endpoints
    public static IServiceCollection AddOutputCachePolicies(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(10)));
            options.AddPolicy("ShortCache", builder => builder.Expire(TimeSpan.FromSeconds(30)).Tag("short"));
            options.AddPolicy("MediumCache", builder => builder.Expire(TimeSpan.FromMinutes(2)).Tag("medium"));
            options.AddPolicy("MatchList", builder => builder
                .Expire(TimeSpan.FromSeconds(15))
                .SetVaryByQuery("page", "pageSize", "creatorId", "status", "teamId")
                .Tag("matches"));
            options.AddPolicy("MatchDetail", builder => builder
                .Expire(TimeSpan.FromSeconds(10))
                .SetVaryByRouteValue("id")
                .Tag("matches"));
            options.AddPolicy("TournamentList", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("page", "pageSize")
                .SetVaryByHeader("Authorization")
                .Tag("tournaments"));
            // PERF: New policies for previously uncached endpoints
            options.AddPolicy("TournamentDetail", builder => builder
                .Expire(TimeSpan.FromSeconds(15))
                .SetVaryByRouteValue("id")
                .Tag("tournaments"));
            options.AddPolicy("TeamList", builder => builder
                .Expire(TimeSpan.FromSeconds(20))
                .SetVaryByQuery("page", "pageSize", "captainId", "playerId")
                .Tag("teams"));
            options.AddPolicy("TeamDetail", builder => builder
                .Expire(TimeSpan.FromSeconds(15))
                .SetVaryByRouteValue("id")
                .Tag("teams"));
            options.AddPolicy("SearchResults", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("q", "page", "pageSize")
                .Tag("search"));
            options.AddPolicy("Analytics", builder => builder
                .Expire(TimeSpan.FromSeconds(45))
                .SetVaryByQuery("teamId")
                .Tag("analytics"));
            options.AddPolicy("Standings", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("page", "pageSize", "groupId")
                .SetVaryByRouteValue("id")
                .Tag("standings"));
        });
        return services;
    }

    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // CORS
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (environment.IsProduction() && (allowedOrigins == null || allowedOrigins.Length == 0))
        {
            throw new InvalidOperationException("AllowedOrigins is not configured. This is required for Production.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend",
                policy =>
                {
                    var origins = new[]
                    {
                        "https://korazone365.com",
                        "https://www.korazone365.com",
                        "https://korazone365.vercel.app",
                        "https://korazone365-gsa2ftfkh9atbuaw.francecentral-01.azurewebsites.net",
                        "http://localhost:4200"
                    };

                    policy.WithOrigins(origins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
        });
        return services;
    }

    // Rate Limiting — PERF-FIX: Add GlobalLimiter so all endpoints are protected
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global limiter: 100 requests/min per user or IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? context.Connection.RemoteIpAddress?.ToString()
                                  ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));

            // Strict limiter for auth endpoints: 10 requests/min
            options.AddFixedWindowLimiter(policyName: "auth", options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 0;
            });
        });
        return services;
    }

    // Swagger
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Kora Zone 365 API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });
        });
        return services;
    }

    // Authentication
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["JwtSettings:Issuer"],
                    ValidAudience = configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSettings:Secret"]!)),
                    ClockSkew = TimeSpan.Zero // PROD-AUDIT: Strict expiration
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userCache = context.HttpContext.RequestServices.GetRequiredService<Application.Interfaces.IUserCacheService>();
                        var userService = context.HttpContext.RequestServices.GetRequiredService<IRepository<User>>();

                        var userIdStr = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var tokenVersionStr = context.Principal?.FindFirst("token_version")?.Value;

                        if (Guid.TryParse(userIdStr, out var userId) && int.TryParse(tokenVersionStr, out var tokenVersion))
                        {
                            // 1. Try Cache First
                            var cachedUser = await userCache.GetUserAsync(userId);

                            if (cachedUser != null)
                            {
                                if (cachedUser.TokenVersion != tokenVersion || cachedUser.Status == UserStatus.Suspended)
                                {
                                    context.Fail("Token is no longer valid.");
                                    return;
                                }

                                // Populate Accessor from Cache to save DB trip in services
                                var userAccessor = context.HttpContext.RequestServices.GetRequiredService<Application.Interfaces.ICurrentUserAccessor>();
                                userAccessor.SetUser(new User
                                {
                                    Id = userId,
                                    Name = cachedUser.Name,
                                    Role = Enum.Parse<UserRole>(cachedUser.Role),
                                    Status = cachedUser.Status,
                                    TokenVersion = cachedUser.TokenVersion
                                });
                                return;
                            }

                            // 2. Fallback to DB
                            var user = await userService.GetByIdNoTrackingAsync(userId, Array.Empty<string>());
                            if (user == null || user.TokenVersion != tokenVersion || user.Status == UserStatus.Suspended)
                            {
                                context.Fail("Token is no longer valid.");
                            }
                            else
                            {
                                // 3. Re-seed cache
                                await userCache.SetUserAsync(userId, user);

                                var userAccessor = context.HttpContext.RequestServices.GetRequiredService<Application.Interfaces.ICurrentUserAccessor>();
                                userAccessor.SetUser(user);
                            }
                        }
                        else
                        {
                            context.Fail("Invalid token claims.");
                        }
                    },
                    // SignalR Token Config
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs/notifications") || path.StartsWithSegments("/hubs/chat")))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        return services;
    }

    // Authorization policies
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole(UserRole.Admin.ToString()));
            options.AddPolicy("RequireCreator", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.TournamentCreator.ToString()));
            options.AddPolicy("RequirePlayer", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.TournamentCreator.ToString(), UserRole.Player.ToString()));
            options.AddPolicy("RequireTeamCaptain", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new global::Infrastructure.Authorization.TeamCaptainRequirement());
            });
            options.AddPolicy("RequireTournamentOwner", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new global::Infrastructure.Authorization.TournamentOwnerRequirement());
            });
        });
        return services;
    }

    // SignalR with optional Redis backplane
    // Note: Redis connectivity for SignalR is deferred — the IConnectionMultiplexer singleton
    // is registered by DependencyInjection.AddInfrastructure(). SignalR reuses that connection
    // instead of opening a separate probe. This method runs BEFORE AddInfrastructure, so we
    // use a post-configure approach: always register SignalR+MessagePack, then conditionally
    // add the Redis backplane in a hosted service or via the connection string.
    public static WebApplicationBuilder AddSignalRServices(this WebApplicationBuilder builder)
    {
        // Read from the single source of truth: ConnectionStrings:Redis
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        bool redisConfigured = !string.IsNullOrWhiteSpace(redisConnectionString);

        if (redisConfigured)
        {
            try
            {
                builder.Services.AddSignalR()
                    .AddMessagePackProtocol()
                    .AddStackExchangeRedis(redisConnectionString!, options => {
                        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("ramadan_signalr");
                    });
                Log.Information("[CONFIG] SignalR backplane: Redis");
            }
            catch (Exception ex)
            {
                builder.Services.AddSignalR().AddMessagePackProtocol();
                Log.Warning(ex, "[CONFIG] SignalR Redis backplane registration failed. Using in-memory backplane.");
            }
        }
        else
        {
            builder.Services.AddSignalR().AddMessagePackProtocol();
            Log.Information("[CONFIG] SignalR backplane: In-Memory");
        }

        builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Api.Infrastructure.CustomUserIdProvider>();
        builder.Services.AddScoped<Application.Interfaces.IRealTimeNotifier, Api.Services.RealTimeNotifier>();
        return builder;
    }
}

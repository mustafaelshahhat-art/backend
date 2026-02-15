using Api.Middleware;
using Application;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Domain.Interfaces;
using Domain.Entities;
using Shared.Exceptions;

using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// PROD-AUDIT: Global Request Size Limit
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

// PROD-AUDIT: Structured Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// PROD-AUDIT: Fail fast if secrets are missing
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 16)
{
    throw new InvalidOperationException("CRITICAL: JwtSettings:Secret is missing or too short. Configure via UserSecrets or Env Vars.");
}

// PROD-AUDIT: Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// PROD-AUDIT: Health Checks (Database)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Infrastructure.Data.AppDbContext>();
builder.Services.AddMemoryCache();

// SignalR with optional Redis backplane
try
{
    var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrEmpty(redisConnectionString) && builder.Environment.IsProduction())
    {
        // Attempt to configure Redis backplane for SignalR
        var checkOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
        checkOptions.AbortOnConnectFail = true;
        checkOptions.ConnectTimeout = 3000;
        using var testConn = StackExchange.Redis.ConnectionMultiplexer.Connect(checkOptions);
        
        if (testConn.IsConnected)
        {
            builder.Services.AddSignalR().AddStackExchangeRedis(redisConnectionString, options => {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("ramadan_signalr");
            });
            Log.Information("SignalR configured with Redis backplane.");
        }
        else
        {
            builder.Services.AddSignalR(); // Fallback to in-memory
            Log.Warning("Redis unavailable for SignalR. Using in-memory backplane.");
        }
    }
    else
    {
        builder.Services.AddSignalR(); // Development or Redis not configured
    }
}
catch
{
    builder.Services.AddSignalR(); // Fallback to in-memory
    Log.Warning("Failed to configure Redis for SignalR. Using in-memory backplane.");
}
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Api.Infrastructure.CustomUserIdProvider>();
builder.Services.AddScoped<Application.Interfaces.IRealTimeNotifier, Api.Services.RealTimeNotifier>();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Strict;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (builder.Environment.IsProduction() && (allowedOrigins == null || allowedOrigins.Length == 0))
{
    throw new InvalidOperationException("AllowedOrigins is not configured. This is required for Production.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true) // Allow any origin in Dev (LAN access)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
            else
            {
                if (allowedOrigins != null && allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }
                
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(policyName: "fixed", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 2;
    });
});

// Add Layer Dependencies
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Helpers
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Application.Interfaces.ICurrentUserAccessor, Infrastructure.Authentication.CurrentUserAccessor>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ramadan Tournament API", Version = "v1" });
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

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!)),
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
                        userAccessor.SetUser(new User { 
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

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole(UserRole.Admin.ToString()));
    options.AddPolicy("RequireCreator", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.TournamentCreator.ToString()));
    options.AddPolicy("RequirePlayer", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.TournamentCreator.ToString(), UserRole.Player.ToString()));
    options.AddPolicy("RequireTeamCaptain", policy => 
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new Infrastructure.Authorization.TeamCaptainRequirement());
    });
    options.AddPolicy("RequireTournamentOwner", policy => 
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new Infrastructure.Authorization.TournamentOwnerRequirement());
    });
});

var app = builder.Build();

// PROD-AUDIT: Redis Health Guard (Optional)
// Check Redis connectivity but don't fail startup if unavailable
try
{
    var redis = app.Services.GetService<StackExchange.Redis.IConnectionMultiplexer>();
    if (redis != null && redis.IsConnected)
    {
        var db = redis.GetDatabase();
        db.Ping();
        Log.Information("Redis connectivity verified.");
    }
    else
    {
        Log.Warning("Redis is not configured or unreachable. Using SQL-based caching and distributed lock.");
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Redis health check failed. Using SQL-based fallback for caching and distributed lock.");
}

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.UseResponseCompression();

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:;");
    await next();
});

//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}
//else 
//{
//    // PROD-AUDIT: Disable detailed exceptions
//    app.UseExceptionHandler("/error");
//    app.UseHsts();
//}

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("AllowFrontend");

app.UseCookiePolicy();
app.UseMiddleware<CorrelationIdMiddleware>(); // PROD-AUDIT: Traceability
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<SlowQueryMiddleware>(); // PROD-AUDIT: Performance
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["X-Correlation-ID"]);
    };
});

app.UseStaticFiles();
app.UseMiddleware<FallbackImageMiddleware>();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseAuthentication();
app.UseMiddleware<UserStatusCheckMiddleware>();
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseRateLimiter(); // Enable Rate Limiting
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Just check if the app is alive
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || true // Check DB, etc.
});
app.MapHub<Api.Hubs.NotificationHub>("/hubs/notifications");
app.MapHub<Api.Hubs.MatchChatHub>("/hubs/chat");

// Ensure Migration?

// Ensure Migration?
// Scope for migration.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    
    try 
    {
        dbContext.Database.Migrate();

        // Seed Admin if not exists (check ignoring soft-delete filters)
        var adminUser = dbContext.Users.IgnoreQueryFilters().FirstOrDefault(u => u.Email == "admin@test.com");
        
        // Retrieve Admin Password safely
        var adminPassword = configuration["AdminSettings:Password"];
        
        // Only enforce password presence if we actully need to create/update the admin
        // But for "Fail startup if missing" rule, we should check it if we intend to seed.
        
        if (adminUser == null)
        {
            if (string.IsNullOrEmpty(adminPassword))
            {
                throw new InvalidOperationException("AdminSettings:Password is not configured. Cannot seed Admin user.");
            }

            var hasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();
            adminUser = new Domain.Entities.User
            {
                Email = "admin@test.com",
                Name = "Admin",
                PasswordHash = hasher.HashPassword(adminPassword),
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                IsEmailVerified = true,
                DisplayId = "ADM-001",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(adminUser);
            dbContext.SaveChanges();
        }
        else 
        {
            // Ensure existing admin is active, not deleted.
            // ONLY update password if configured, otherwise keep existing.
            // This prevents locking out admin if config is missing in an existing env, 
            // BUT the audit goal is "Remove Hardcoded". So we can't fall back to "password".
            
            var hasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();
            adminUser.Role = UserRole.Admin;
            adminUser.Status = UserStatus.Active;
            adminUser.IsEmailVerified = true;
            
            if (!string.IsNullOrEmpty(adminPassword))
            {
                adminUser.PasswordHash = hasher.HashPassword(adminPassword);
            }
            
            dbContext.SaveChanges();
        }
        // Initialize Activity Log Migration (Run once or on demand)
        // var migrationService = scope.ServiceProvider.GetRequiredService<Application.Services.ActivityLogMigrationService>();
        // await migrationService.MigrateLegacyLogsAsync();
    }
    catch (Exception ex)
    {
        // Log migration error
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        
        // Rethrow if it's a configuration error to prevent silent failure in Production
        if (ex is InvalidOperationException) throw;
    }
}

app.Run();
// Trigger rebuild for user hard-delete cleanup

public partial class Program { }

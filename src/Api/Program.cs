using Api.Middleware;
using Microsoft.AspNetCore.Mvc;
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
using Microsoft.AspNetCore.StaticFiles;

using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// PROD-AUDIT: Global Request Size Limit + PERF-FIX I2: Kestrel thread tuning
// PERF: Tuned for 256MB shared hosting — lower connection caps to reduce memory pressure
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.MaxConcurrentConnections = 100;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = 50; // WebSockets/SignalR
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
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

// Response Caching service (required for [ResponseCache] attribute support)
builder.Services.AddResponseCaching();

// PERF-FIX I4: Output Caching for read-heavy endpoints
builder.Services.AddOutputCache(options =>
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

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // PERF: Skip null properties — reduces payload size by ~15-25%
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// PROD-AUDIT: Health Checks (Database)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Infrastructure.Data.AppDbContext>();
// PERF: Memory-safe cache — 50MB cap for 256MB shared hosting. Entries MUST set Size.
builder.Services.AddMemoryCache(options => options.SizeLimit = 50 * 1024 * 1024);

// Custom Validation Response
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value != null && e.Value.Errors.Count > 0)
            .Select(e => new {
                Field = e.Key,
                Errors = e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
            }).ToList();

        var result = new {
            code = "VALIDATION_ERROR",
            message = "البيانات المرسلة غير صالحة. يرجى مراجعة الحقول.",
            details = errors
        };
        return new BadRequestObjectResult(result);
    };
});
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
            // Development: explicit localhost origins (no wildcard — safe with credentials)
            var origins = builder.Environment.IsDevelopment()
                ? new[] { "http://localhost:4200", "http://127.0.0.1:4200" }
                : allowedOrigins ?? Array.Empty<string>();

            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Rate Limiting — PERF-FIX: Add GlobalLimiter so all endpoints are protected
builder.Services.AddRateLimiter(options =>
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
app.UseMiddleware<GlobalExceptionHandlerMiddleware>(); // Must be first — catches all downstream exceptions

app.UseHttpsRedirection();
app.UseResponseCompression();

// CORS must be FIRST — before any middleware that can short-circuit (OutputCache, ResponseCaching, auth, etc.)
// Without this, OutputCache serves anonymous cached responses WITHOUT Access-Control-Allow-Origin,
// causing CORS errors for guest users on cached endpoints (e.g. tournament list).
app.UseCors("AllowFrontend");

app.UseResponseCaching();
app.UseOutputCache(); // PERF-FIX I4: Output Caching middleware

// PERF-FIX B12: Removed duplicate inline security headers — consolidated into SecurityHeadersMiddleware

app.UseSwagger();
app.UseSwaggerUI();

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

// Serve static files from wwwroot with correct MIME types and caching
var staticFileProvider = new FileExtensionContentTypeProvider();
// Ensure modern file types are mapped correctly
staticFileProvider.Mappings[".js"] = "application/javascript";
staticFileProvider.Mappings[".mjs"] = "application/javascript";
staticFileProvider.Mappings[".css"] = "text/css";
staticFileProvider.Mappings[".json"] = "application/json";
staticFileProvider.Mappings[".wasm"] = "application/wasm";
staticFileProvider.Mappings[".webmanifest"] = "application/manifest+json";
staticFileProvider.Mappings[".webp"] = "image/webp";
staticFileProvider.Mappings[".avif"] = "image/avif";
staticFileProvider.Mappings[".woff2"] = "font/woff2";
staticFileProvider.Mappings[".woff"] = "font/woff";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileProvider,
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        // Hashed filenames (e.g., main-ICDGF477.js) are immutable — cache aggressively
        if (path.Contains('-') && (path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".woff2")))
        {
            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        }
        else if (path.EndsWith(".html"))
        {
            // Never cache index.html — must always get fresh version to pick up new hashes
            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
        }
        else
        {
            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "public, max-age=86400";
        }
    }
});
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
    Predicate = check => check.Tags.Contains("ready")
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

        // Seed location data (governorates, cities, areas)
        Infrastructure.Data.LocationSeeder.Seed(dbContext, scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
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

using Api.Middleware;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Api.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
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

        return app;
    }

    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
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

        return app;
    }

    public static WebApplication InitializeDatabase(this WebApplication app)
    {
        // Ensure Migration?

        // Ensure Migration?
        // Scope for migration.
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<global::Infrastructure.Data.AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // PROD-DEBUG: connection string check
            var connString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connString)) 
            {
               var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
               logger.LogCritical("CRITICAL: ConnectionString 'DefaultConnection' is MISSING. Skipping migration/seeding to allow debug startup.");
               return app; 
            }

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
                global::Infrastructure.Data.LocationSeeder.Seed(dbContext, scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
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

        return app;
    }
}

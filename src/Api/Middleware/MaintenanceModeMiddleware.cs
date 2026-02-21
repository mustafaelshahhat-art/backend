using System.Security.Claims;
using Application.Interfaces;

namespace Api.Middleware;

/// <summary>
/// Middleware that blocks all non-admin access when maintenance mode is enabled.
/// </summary>
public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that are always allowed even during maintenance
    private static readonly string[] AllowedPaths = new[]
    {
        "/api/v1/status/",
        "/api/v1/auth/login",
        "/api/v1/auth/refresh-token",
        "/health/"
    };

    public MaintenanceModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISystemSettingsService settingsService)
    {
        // Skip for specific paths to avoid infinite loop or blocking critical endpoints
        var path = context.Request.Path.Value?.ToLower();
        if (path != null && (
            path.StartsWith("/api/v1/auth") || 
            path.StartsWith("/api/v1/status") || 
            path.StartsWith("/health") || 
            path.StartsWith("/swagger") ||
            path.StartsWith("/api/v1/debug") // Allow Debug Controller
           ))
        {
            await _next(context);
            return;
        }

        bool isMaintenance = false;
        try 
        {
            isMaintenance = await settingsService.IsMaintenanceModeEnabledAsync();
        }
        catch (Exception ex)
        {
             // PROD-DEBUG: If DB is down (ConnectionString not initialized), treat as NOT maintenance so we can debug
             // Otherwise user sees 500 error instead of useful info or simple failure
             var logger = context.RequestServices.GetService<ILogger<MaintenanceModeMiddleware>>();
             logger?.LogWarning(ex, "Failed to check maintenance mode (likely DB down). Proceeding for debug.");
             isMaintenance = false;
        }

        if (!isMaintenance)
        {
            await _next(context);
            return;
        }

        // Maintenance mode is enabled - check if user is admin
        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
        var isAdmin = isAuthenticated && userRole == "Admin";

        if (isAdmin)
        {
            // Admin can access during maintenance
            await _next(context);
            return;
        }

        // Block non-admin access during maintenance
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Service Unavailable",
            message = "النظام تحت الصيانة حالياً"
        });
    }
}

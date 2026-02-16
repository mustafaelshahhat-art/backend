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
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Always allow certain paths
        if (AllowedPaths.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        // Check if maintenance mode is enabled
        var isMaintenanceMode = await settingsService.IsMaintenanceModeEnabledAsync();
        
        if (!isMaintenanceMode)
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

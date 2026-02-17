using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Api.Middleware;

public class FallbackImageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FallbackImageMiddleware> _logger;

    public FallbackImageMiddleware(RequestDelegate next, ILogger<FallbackImageMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == 404 && IsImageRequest(context.Request.Path))
        {
            await ServePlaceholder(context);
        }
    }

    private bool IsImageRequest(PathString path)
    {
        var value = path.Value?.ToLowerInvariant() ?? string.Empty;
        return value.StartsWith("/uploads/") && 
               (value.EndsWith(".png") || value.EndsWith(".jpg") || value.EndsWith(".jpeg") || value.EndsWith(".webp"));
    }

    private async Task ServePlaceholder(HttpContext context)
    {
        var placeholderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "images", "team-placeholder.png");

        if (File.Exists(placeholderPath))
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/png";
            await context.Response.SendFileAsync(placeholderPath);
            _logger.LogInformation("Served fallback image for: {Path}", context.Request.Path);
        }
    }
}

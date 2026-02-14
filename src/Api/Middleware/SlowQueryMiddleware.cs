using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Api.Middleware;

public class SlowQueryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SlowQueryMiddleware> _logger;
    private const int SlowThresholdMs = 500;

    public SlowQueryMiddleware(RequestDelegate next, ILogger<SlowQueryMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        
        await _next(context);
        
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowThresholdMs)
        {
            var path = context.Request.Path;
            var method = context.Request.Method;
            _logger.LogWarning("Slow Request Detected: {Method} {Path} took {ElapsedMs}ms", method, path, sw.ElapsedMilliseconds);
        }
    }
}

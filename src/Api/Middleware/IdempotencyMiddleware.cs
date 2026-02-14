using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post && context.Request.Method != HttpMethods.Put)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Idempotency-Key", out var idempotencyKey) || string.IsNullOrEmpty(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cancellationToken = context.RequestAborted;
        var route = context.Request.Path.ToString();
        var requestHash = await ComputeRequestHash(context.Request);

        using var scope = context.RequestServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingRequest = await dbContext.IdempotentRequests
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey.ToString() && r.Route == route, cancellationToken);

        if (existingRequest != null)
        {
            if (existingRequest.Status == IdempotencyStatus.InProgress)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsync("A request with the same idempotency key is already in progress.");
                return;
            }

            if (existingRequest.RequestHash != requestHash)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Idempotency key match but request content has changed.");
                return;
            }

            // Replay response
            context.Response.StatusCode = existingRequest.StatusCode ?? 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existingRequest.ResponseBody ?? string.Empty);
            return;
        }

        // Store InProgress
        var newRequest = new IdempotentRequest
        {
            Key = idempotencyKey.ToString(),
            Route = route,
            RequestHash = requestHash,
            Status = IdempotencyStatus.InProgress,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.IdempotentRequests.Add(newRequest);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent request might have just inserted it
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsync("Concurrent request detected.");
            return;
        }

        // Capture response
        var originalResponseBody = context.Response.Body;
        using var responseBodyMemoryStream = new MemoryStream();
        context.Response.Body = responseBodyMemoryStream;

        try
        {
            await _next(context);

            // Update with completed status and response
            responseBodyMemoryStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBodyMemoryStream).ReadToEndAsync();
            responseBodyMemoryStream.Seek(0, SeekOrigin.Begin);

            newRequest.Status = IdempotencyStatus.Completed;
            newRequest.ResponseBody = responseBody;
            newRequest.StatusCode = context.Response.StatusCode;
            newRequest.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            
            await responseBodyMemoryStream.CopyToAsync(originalResponseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during idempotent request processing.");
            newRequest.Status = IdempotencyStatus.Failed;
            newRequest.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private async Task<string> ComputeRequestHash(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hashedBytes);
    }
}

using System.Net;
using System.Text.Json;
using Shared.Exceptions;

namespace Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var statusCode = HttpStatusCode.InternalServerError;
        var errorCode = "INTERNAL_SERVER_ERROR";
        var message = "An unexpected error occurred.";
        object? details = null;

        switch (exception)
        {
            case NotFoundException ex:
                statusCode = HttpStatusCode.NotFound;
                errorCode = "NOT_FOUND";
                message = ex.Message;
                break;
            case BadRequestException ex:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "BAD_REQUEST";
                message = ex.Message;
                break;
            case ValidationException ex:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "VALIDATION_ERROR";
                message = ex.Message;
                details = ex.Errors;
                break;
            case ForbiddenException ex:
                statusCode = HttpStatusCode.Forbidden;
                errorCode = "FORBIDDEN";
                message = ex.Message;
                break;
            case ConflictException ex:
                statusCode = HttpStatusCode.Conflict;
                errorCode = "CONFLICT";
                message = ex.Message;
                break;
            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                errorCode = "UNAUTHORIZED";
                message = "Unauthorized access.";
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            code = errorCode,
            message = message,
            details = details
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

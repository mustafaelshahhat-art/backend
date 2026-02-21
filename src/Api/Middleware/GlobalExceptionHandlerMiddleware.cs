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

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var statusCode = HttpStatusCode.InternalServerError;
        var errorCode = "INTERNAL_SERVER_ERROR";
        var message = "حدث خطأ غير متوقع أثناء معالجة الطلب. يرجى المحاولة مرة أخرى بعد قليل.";
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
                _logger.LogWarning("Bad Request: {Message}", ex.Message);
                break;
            case ValidationException ex:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "VALIDATION_ERROR";
                message = ex.Message;
                details = ex.Errors;
                
                var currentEnv = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
                if (currentEnv.IsDevelopment())
                {
                    _logger.LogWarning("Validation failed: {Message}. Errors: {Errors}", 
                        ex.Message, 
                        JsonSerializer.Serialize(ex.Errors));
                }
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
            case EmailNotVerifiedException ex:
                statusCode = HttpStatusCode.Unauthorized;
                errorCode = "EMAIL_NOT_VERIFIED";
                message = ex.Message;
                details = new { email = ex.Email };
                break;
            case UnauthorizedAccessException ex:
                statusCode = HttpStatusCode.Unauthorized;
                errorCode = "UNAUTHORIZED";
                message = ex.Message;
                break;
            case ArgumentException ex:
                statusCode = HttpStatusCode.BadRequest;
                errorCode = "BAD_REQUEST";
                message = ex.Message;
                _logger.LogWarning("Argument error: {Message}", ex.Message);
                break;
            case InvalidOperationException ex:
                statusCode = HttpStatusCode.Conflict;
                errorCode = "CONFLICT";
                message = ex.Message;
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        
        var response = new
        {
            code = errorCode,
            message = message,
            // PRODUCTION DEBUG: Force details
            details = details ?? new { Error = exception.Message, Stack = exception.StackTrace }
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

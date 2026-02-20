using System.Text.Json.Serialization;

namespace Application.Contracts.Common;

/// <summary>
/// Standardized error response contract.
/// ALL errors from GlobalExceptionHandlerMiddleware return this shape.
/// 
/// {
///   "code": "NOT_FOUND",
///   "message": "Resource not found.",
///   "details": null
/// }
/// </summary>
public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; set; }

    public ErrorResponse() { }

    public ErrorResponse(string code, string message, object? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }
}

/// <summary>
/// Validation error detail shape.
/// </summary>
public class ValidationErrorResponse : ErrorResponse
{
    public new Dictionary<string, string[]> Details { get; set; } = new();

    public ValidationErrorResponse(string message, Dictionary<string, string[]> errors)
        : base("VALIDATION_ERROR", message, errors)
    {
        Details = errors;
    }
}

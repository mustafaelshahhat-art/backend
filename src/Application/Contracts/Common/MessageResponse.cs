namespace Application.Contracts.Common;

/// <summary>
/// Standardized success message response.
/// Used instead of anonymous { message = "..." } objects.
/// </summary>
public class MessageResponse
{
    public string Message { get; set; } = string.Empty;

    public MessageResponse() { }
    public MessageResponse(string message) => Message = message;
}

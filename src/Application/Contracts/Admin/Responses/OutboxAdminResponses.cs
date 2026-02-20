namespace Application.Contracts.Admin.Responses;

/// <summary>
/// Dead letter messages list response.
/// Replaces anonymous { messages, totalCount }.
/// </summary>
[Obsolete("Use PagedResult<DeadLetterMessageDto> instead.")]
public class DeadLetterListResponse
{
    public List<DeadLetterMessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Dead letter message DTO.
/// No domain entity exposure — projects only safe fields.
/// </summary>
public class DeadLetterMessageDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}

/// <summary>
/// Retry dead letter response.
/// Replaces anonymous { message }.
/// </summary>
public class RetryDeadLetterResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Clear dead letters response.
/// Replaces anonymous { count, message }.
/// </summary>
public class ClearDeadLettersResponse
{
    public int Count { get; set; }
    public string Message { get; set; } = string.Empty;
}

using Domain.Enums;
using System;

namespace Domain.Entities;

public class OutboxMessage : BaseEntity
{
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime? ProcessedOn { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public string? DeadLetterReason { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    DeadLetter = 4
}

using System;

namespace Domain.Entities;

public class IdempotentRequest : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.InProgress;
    public string? ResponseBody { get; set; }
    public int? StatusCode { get; set; }
}

public enum IdempotencyStatus
{
    InProgress = 0,
    Completed = 1,
    Failed = 2
}

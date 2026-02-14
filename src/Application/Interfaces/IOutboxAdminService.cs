using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IOutboxAdminService
{
    Task<(IEnumerable<OutboxMessage> Messages, int TotalCount)> GetDeadLetterMessagesAsync(int page, int pageSize, CancellationToken ct);
}

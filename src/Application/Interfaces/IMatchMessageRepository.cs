using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IMatchMessageRepository
{
    Task<MatchMessage> AddAsync(MatchMessage message, CancellationToken ct = default);
    Task<IEnumerable<MatchMessage>> GetByMatchIdAsync(Guid matchId, int pageSize = 50, int page = 1, CancellationToken ct = default);
}

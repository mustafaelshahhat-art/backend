using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface IMatchService
{
    Task<Application.Common.Models.PagedResult<MatchDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? creatorId = null, CancellationToken ct = default);
    Task<MatchDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MatchDto> StartMatchAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    Task<MatchDto> EndMatchAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    Task<MatchDto> AddEventAsync(Guid id, AddMatchEventRequest request, Guid userId, string userRole, CancellationToken ct = default);
    Task<MatchDto> RemoveEventAsync(Guid matchId, Guid eventId, Guid userId, string userRole, CancellationToken ct = default);

    Task<MatchDto> UpdateAsync(Guid id, UpdateMatchRequest request, Guid userId, string userRole, CancellationToken ct = default);

    Task<IEnumerable<MatchDto>> GenerateMatchesForTournamentAsync(Guid tournamentId, CancellationToken ct = default);

}

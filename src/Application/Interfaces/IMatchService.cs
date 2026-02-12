using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface IMatchService
{
    Task<IEnumerable<MatchDto>> GetAllAsync();
    Task<MatchDto?> GetByIdAsync(Guid id);
    Task<MatchDto> StartMatchAsync(Guid id, Guid userId, string userRole);
    Task<MatchDto> EndMatchAsync(Guid id, Guid userId, string userRole);
    Task<MatchDto> AddEventAsync(Guid id, AddMatchEventRequest request, Guid userId, string userRole);
    Task<MatchDto> RemoveEventAsync(Guid matchId, Guid eventId, Guid userId, string userRole);

    Task<MatchDto> UpdateAsync(Guid id, UpdateMatchRequest request, Guid userId, string userRole);

    Task<IEnumerable<MatchDto>> GenerateMatchesForTournamentAsync(Guid tournamentId);

}

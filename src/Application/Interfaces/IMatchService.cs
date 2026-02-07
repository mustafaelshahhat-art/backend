using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface IMatchService
{
    Task<IEnumerable<MatchDto>> GetAllAsync();
    Task<MatchDto?> GetByIdAsync(Guid id);
    Task<MatchDto> StartMatchAsync(Guid id);
    Task<MatchDto> EndMatchAsync(Guid id);
    Task<MatchDto> AddEventAsync(Guid id, AddMatchEventRequest request);
    Task<MatchDto> RemoveEventAsync(Guid matchId, Guid eventId);
    Task<MatchDto> SubmitReportAsync(Guid id, SubmitReportRequest request);
    Task<MatchDto> UpdateAsync(Guid id, UpdateMatchRequest request);

    Task<IEnumerable<MatchDto>> GenerateMatchesForTournamentAsync(Guid tournamentId);
    Task<IEnumerable<MatchDto>> GetMatchesByRefereeAsync(Guid refereeId);
}

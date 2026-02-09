using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface ITournamentService
{
    Task<IEnumerable<TournamentDto>> GetAllAsync();
    Task<TournamentDto?> GetByIdAsync(Guid id);
    Task<TournamentDto> CreateAsync(CreateTournamentRequest request);
    Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request);
    Task DeleteAsync(Guid id);
    
    Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId);
    Task<IEnumerable<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId);
    Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId);
    Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId);
    Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request);
    
    Task<IEnumerable<PendingPaymentResponse>> GetPendingPaymentsAsync();
    Task<IEnumerable<PendingPaymentResponse>> GetAllPaymentRequestsAsync();
    Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId);
    Task<IEnumerable<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId, int? groupId = null);
    Task<IEnumerable<GroupDto>> GetGroupsAsync(Guid tournamentId);
    Task<BracketDto> GetBracketAsync(Guid tournamentId);
    Task<TournamentDto> CloseRegistrationAsync(Guid id);
    Task EliminateTeamAsync(Guid tournamentId, Guid teamId);
}

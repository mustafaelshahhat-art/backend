using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface ITournamentService
{
    Task<IEnumerable<TournamentDto>> GetAllAsync(Guid? creatorId = null);
    Task<TournamentDto?> GetByIdAsync(Guid id);
    Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid? creatorId = null);
    Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId, string userRole);
    Task DeleteAsync(Guid id, Guid userId, string userRole);
    
    Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId);
    Task<IEnumerable<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId);
    Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId);
    Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole);
    Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request, Guid userId, string userRole);
    
    Task<IEnumerable<PendingPaymentResponse>> GetPendingPaymentsAsync(Guid? creatorId = null);
    Task<IEnumerable<PendingPaymentResponse>> GetAllPaymentRequestsAsync(Guid? creatorId = null);
    Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId, Guid userId, string userRole);
    Task<IEnumerable<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId, int? groupId = null);
    Task<IEnumerable<GroupDto>> GetGroupsAsync(Guid tournamentId);
    Task<BracketDto> GetBracketAsync(Guid tournamentId);
    Task<TournamentDto> CloseRegistrationAsync(Guid id, Guid userId, string userRole);
    Task EliminateTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole);
    Task<TournamentDto> EmergencyStartAsync(Guid id, Guid userId, string userRole);
    Task<TournamentDto> EmergencyEndAsync(Guid id, Guid userId, string userRole);
}

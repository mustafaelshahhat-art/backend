using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;

namespace Application.Interfaces;

public interface ITournamentService
{

    Task<Application.Common.Models.PagedResult<TournamentDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? creatorId = null, CancellationToken ct = default);
    Task<TournamentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid? creatorId = null, CancellationToken ct = default);
    Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId, string userRole, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    
    Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId, CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId, int page, int pageSize, CancellationToken ct = default);
    Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId, CancellationToken ct = default);
    Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default);
    Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request, Guid userId, string userRole, CancellationToken ct = default);
    Task WithdrawTeamAsync(Guid tournamentId, Guid teamId, Guid userId, CancellationToken ct = default);
    Task<TeamRegistrationDto> PromoteWaitingTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default);
    
    Task<Application.Common.Models.PagedResult<PendingPaymentResponse>> GetPendingPaymentsAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<PendingPaymentResponse>> GetAllPaymentRequestsAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default);
    Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId, Guid userId, string userRole, CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId, int page, int pageSize, int? groupId = null, CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<GroupDto>> GetGroupsAsync(Guid tournamentId, int page, int pageSize, CancellationToken ct = default);
    Task<BracketDto> GetBracketAsync(Guid tournamentId, CancellationToken ct = default);
    Task<TournamentDto> CloseRegistrationAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    Task ProcessAutomatedStateTransitionsAsync(CancellationToken ct = default);
    Task EliminateTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default);
    Task<TournamentDto> EmergencyStartAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    Task<TournamentDto> EmergencyEndAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    
    Task<IEnumerable<MatchDto>> SetOpeningMatchAsync(Guid tournamentId, Guid homeTeamId, Guid awayTeamId, Guid userId, string userRole, CancellationToken ct = default);
    Task<IEnumerable<MatchDto>> GenerateManualMatchesAsync(Guid tournamentId, ManualDrawRequest request, Guid userId, string userRole, CancellationToken ct = default);
}

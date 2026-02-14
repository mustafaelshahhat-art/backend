using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Teams;

namespace Application.Interfaces;

public interface ITeamService
{
    Task<Application.Common.Models.PagedResult<TeamDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? captainId = null, Guid? playerId = null, CancellationToken ct = default);
    Task<TeamDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId, CancellationToken ct = default);
    Task<TeamDto> UpdateAsync(Guid id, UpdateTeamRequest request, Guid userId, string userRole, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default);
    
    Task<JoinRequestDto> RequestJoinAsync(Guid teamId, Guid playerId, CancellationToken ct = default);
    Task<IEnumerable<JoinRequestDto>> GetJoinRequestsAsync(Guid teamId, CancellationToken ct = default);
    Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve, Guid userId, string userRole, CancellationToken ct = default);
    
    // Invitation flow
    Task<JoinRequestDto> InvitePlayerAsync(Guid teamId, Guid captainId, AddPlayerRequest request, CancellationToken ct = default);
    Task<JoinRequestDto> AcceptInviteAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<JoinRequestDto> RejectInviteAsync(Guid requestId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<JoinRequestDto>> GetUserInvitationsAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<JoinRequestDto>> GetRequestsForCaptainAsync(Guid captainId, CancellationToken ct = default);
    
    Task RemovePlayerAsync(Guid teamId, Guid playerId, Guid userId, string userRole, CancellationToken ct = default);

    Task<IEnumerable<PlayerDto>> GetTeamPlayersAsync(Guid teamId, CancellationToken ct = default);
    Task<IEnumerable<Application.DTOs.Matches.MatchDto>> GetTeamMatchesAsync(Guid teamId, CancellationToken ct = default);
    Task<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>> GetTeamFinancialsAsync(Guid teamId, CancellationToken ct = default);

    // Admin Action
    Task DisableTeamAsync(Guid teamId, CancellationToken ct = default);
    Task ActivateTeamAsync(Guid teamId, CancellationToken ct = default);
    
    // Multi-team support
    Task<TeamsOverviewDto> GetTeamsOverviewAsync(Guid userId, CancellationToken ct = default);
}

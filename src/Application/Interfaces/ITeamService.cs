using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Teams;

namespace Application.Interfaces;

public interface ITeamService
{
    Task<IEnumerable<TeamDto>> GetAllAsync(Guid? captainId = null, Guid? playerId = null);
    Task<TeamDto?> GetByIdAsync(Guid id);
    Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId);
    Task<TeamDto> UpdateAsync(Guid id, UpdateTeamRequest request, Guid userId, string userRole);
    Task DeleteAsync(Guid id, Guid userId, string userRole);
    
    Task<JoinRequestDto> RequestJoinAsync(Guid teamId, Guid playerId);
    Task<IEnumerable<JoinRequestDto>> GetJoinRequestsAsync(Guid teamId);
    Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve, Guid userId, string userRole);
    
    // Invitation flow
    Task<JoinRequestDto> InvitePlayerAsync(Guid teamId, Guid captainId, AddPlayerRequest request);
    Task<JoinRequestDto> AcceptInviteAsync(Guid requestId, Guid userId);
    Task<JoinRequestDto> RejectInviteAsync(Guid requestId, Guid userId);
    Task<IEnumerable<JoinRequestDto>> GetUserInvitationsAsync(Guid userId);
    Task<IEnumerable<JoinRequestDto>> GetRequestsForCaptainAsync(Guid captainId);
    
    Task RemovePlayerAsync(Guid teamId, Guid playerId, Guid userId, string userRole);

    Task<IEnumerable<PlayerDto>> GetTeamPlayersAsync(Guid teamId);
    Task<IEnumerable<Application.DTOs.Matches.MatchDto>> GetTeamMatchesAsync(Guid teamId);
    Task<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>> GetTeamFinancialsAsync(Guid teamId);

    // Admin Action
    Task DisableTeamAsync(Guid teamId);
    Task ActivateTeamAsync(Guid teamId);
    
    // Multi-team support
    Task<TeamsOverviewDto> GetTeamsOverviewAsync(Guid userId);
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Teams;

namespace Application.Interfaces;

public interface ITeamService
{
    Task<IEnumerable<TeamDto>> GetAllAsync();
    Task<TeamDto?> GetByIdAsync(Guid id);
    Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId);
    Task<TeamDto> UpdateAsync(Guid id, UpdateTeamRequest request);
    Task DeleteAsync(Guid id);
    
    Task<JoinRequestDto> RequestJoinAsync(Guid teamId, Guid playerId);
    Task<IEnumerable<JoinRequestDto>> GetJoinRequestsAsync(Guid teamId);
    Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve);
    
    // Invitation flow
    Task<JoinRequestDto> InvitePlayerAsync(Guid teamId, Guid captainId, AddPlayerRequest request);
    Task<JoinRequestDto> AcceptInviteAsync(Guid requestId, Guid userId);
    Task<JoinRequestDto> RejectInviteAsync(Guid requestId, Guid userId);
    Task<IEnumerable<JoinRequestDto>> GetUserInvitationsAsync(Guid userId);
    Task<IEnumerable<JoinRequestDto>> GetRequestsForCaptainAsync(Guid captainId);
    
    Task RemovePlayerAsync(Guid teamId, Guid playerId);

    Task<IEnumerable<PlayerDto>> GetTeamPlayersAsync(Guid teamId);
    Task<IEnumerable<Application.DTOs.Matches.MatchDto>> GetTeamMatchesAsync(Guid teamId);
    Task<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>> GetTeamFinancialsAsync(Guid teamId);
}

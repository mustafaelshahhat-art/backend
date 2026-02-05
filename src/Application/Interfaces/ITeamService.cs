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
    
    Task<PlayerDto> AddPlayerAsync(Guid teamId, AddPlayerRequest request);
    Task RemovePlayerAsync(Guid teamId, Guid playerId);
}

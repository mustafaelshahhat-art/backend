using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Teams;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using Shared.Exceptions;
using Domain.Enums; // If needed

namespace Application.Services;

public class TeamService : ITeamService
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;

    public TeamService(
        IRepository<Team> teamRepository,
        IRepository<User> userRepository,
        IRepository<Player> playerRepository,
        IRepository<TeamJoinRequest> joinRequestRepository,
        IMapper mapper,
        IAnalyticsService analyticsService)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _playerRepository = playerRepository;
        _joinRequestRepository = joinRequestRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
    }

    public async Task<IEnumerable<TeamDto>> GetAllAsync()
    {
        var teams = await _teamRepository.GetAllAsync(t => t.Captain!, t => t.Players);
        return _mapper.Map<IEnumerable<TeamDto>>(teams);
    }

    public async Task<TeamDto?> GetByIdAsync(Guid id)
    {
        var team = await _teamRepository.GetByIdAsync(id, t => t.Captain!, t => t.Players);
        return team == null ? null : _mapper.Map<TeamDto>(team);
    }

    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId)
    {
        var captain = await _userRepository.GetByIdAsync(captainId);
        if (captain == null) throw new NotFoundException(nameof(User), captainId);

        if (captain.TeamId.HasValue) 
            throw new ConflictException("المستخدم يملك فريقاً بالفعل.");

        var team = new Team
        {
            Name = request.Name,
            CaptainId = captainId,
            Founded = request.Founded,
            City = request.City,
            Logo = request.Logo,
            Players = new List<Player>()
        };

        // Create player record for captain
        var player = new Player
        {
            Name = captain.Name,
            DisplayId = "P-" + captain.DisplayId.Replace("U-", ""),
            UserId = captainId
        };
        team.Players.Add(player);

        // Save team (will save players too due to relationship)
        await _teamRepository.AddAsync(team);

        // Link User to Team
        captain.TeamId = team.Id;
        await _userRepository.UpdateAsync(captain);

        await _analyticsService.LogActivityAsync("Team Created", $"Team {team.Name} created by {captain.Name}", captainId, captain.Name);
        
        var dto = _mapper.Map<TeamDto>(team);
        dto.CaptainName = captain.Name; // Ensure it's populated for the immediate response
        return dto;
    }

    public async Task<TeamDto> UpdateAsync(Guid id, UpdateTeamRequest request)
    {
        var team = await _teamRepository.GetByIdAsync(id);
        if (team == null) throw new NotFoundException(nameof(Team), id);

        if (!string.IsNullOrEmpty(request.Name)) team.Name = request.Name!;
        if (!string.IsNullOrEmpty(request.City)) team.City = request.City;
        if (!string.IsNullOrEmpty(request.Logo)) team.Logo = request.Logo;

        await _teamRepository.UpdateAsync(team);
        return _mapper.Map<TeamDto>(team);
    }

    public async Task DeleteAsync(Guid id)
    {
        // Get all users associated with this team
        var users = await _userRepository.FindAsync(u => u.TeamId == id);
        foreach (var user in users)
        {
            user.TeamId = null;
            await _userRepository.UpdateAsync(user);
        }

        await _teamRepository.DeleteAsync(id);
    }

    public async Task<JoinRequestDto> RequestJoinAsync(Guid teamId, Guid playerId)
    {
        // playerId here likely refers to User.Id (Candidate)
        var existingRequest = await _joinRequestRepository.FindAsync(r => r.TeamId == teamId && r.UserId == playerId && r.Status == "pending");
        if (existingRequest.Any())
        {
            return new JoinRequestDto
            {
                Id = existingRequest.First().Id,
                PlayerId = playerId,
                Status = "pending",
                RequestDate = existingRequest.First().CreatedAt
            };
        }

        var user = await _userRepository.GetByIdAsync(playerId);
        if (user == null) throw new NotFoundException(nameof(User), playerId);

        var request = new TeamJoinRequest
        {
            TeamId = teamId,
            UserId = playerId,
            Status = "pending"
        };

        await _joinRequestRepository.AddAsync(request);

        var result = new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = playerId,
            PlayerName = user.Name,
            Status = "pending",
            RequestDate = request.CreatedAt
        };

        await _analyticsService.LogActivityAsync("Join Request", $"User {user.Name} requested to join team id {teamId}", playerId, user.Name);
        return result;
    }

    public async Task<IEnumerable<JoinRequestDto>> GetJoinRequestsAsync(Guid teamId)
    {
        var requests = await _joinRequestRepository.FindAsync(r => r.TeamId == teamId);
        // Need to load user for name mapping?
        // Mapping loop manually or mapper.
        var dtos = new List<JoinRequestDto>();
        foreach (var r in requests)
        {
             // inefficient N+1 but ok for MVP
             var u = await _userRepository.GetByIdAsync(r.UserId);
             dtos.Add(new JoinRequestDto
             {
                 Id = r.Id,
                 PlayerId = r.UserId,
                 PlayerName = u?.Name ?? "Unknown",
                 Status = r.Status,
                 RequestDate = r.CreatedAt
             });
        }
        return dtos;
    }

    public async Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);

        if (approve)
        {
            request.Status = "approved";
             // Add player to team
             var user = await _userRepository.GetByIdAsync(request.UserId);
             if (user != null) {
                 user.TeamId = teamId;
                 await _userRepository.UpdateAsync(user);

                 var player = new Player
                 {
                     Name = user.Name,
                     DisplayId = "P-" + user.DisplayId,
                     UserId = user.Id,
                     TeamId = teamId
                 };
                 await _playerRepository.AddAsync(player);
             }
        }
        else
        {
            request.Status = "rejected";
        }

        await _joinRequestRepository.UpdateAsync(request);

         // Helper to return DTO
         var u = await _userRepository.GetByIdAsync(request.UserId);
         return new JoinRequestDto
         {
             Id = request.Id,
             PlayerId = request.UserId,
             PlayerName = u?.Name ?? "Unknown",
             Status = request.Status,
             RequestDate = request.CreatedAt
         };
    }

    public async Task<PlayerDto> AddPlayerAsync(Guid teamId, AddPlayerRequest request)
    {
        // Search user by DisplayId?
        var users = await _userRepository.FindAsync(u => u.DisplayId == request.DisplayId);
        var user = users.FirstOrDefault();
        
        // If user not found, maybe just add by name (requires changing Request DTO to support name)?
        // Contract says "Add player by Display ID".
        if (user == null)
        {
             throw new NotFoundException("User not found with Display ID " + request.DisplayId);
        }

        var player = new Player
        {
            Name = user.Name,
            DisplayId = "P-" + user.DisplayId,
            UserId = user.Id,
            TeamId = teamId
        };

        await _playerRepository.AddAsync(player);
        return _mapper.Map<PlayerDto>(player);
    }

    public async Task RemovePlayerAsync(Guid teamId, Guid playerId)
    {
        // playerId here is Player Entity Id? Yes from route {playerId} in team.
        var player = await _playerRepository.GetByIdAsync(playerId);
        // Safety check teamId
        if (player != null && player.TeamId == teamId)
        {
            await _playerRepository.DeleteAsync(player);
        }
    }
}

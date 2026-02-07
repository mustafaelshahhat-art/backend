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
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly ISystemSettingsService _systemSettingsService;

    public TeamService(
        IRepository<Team> teamRepository,
        IRepository<User> userRepository,
        IRepository<Player> playerRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamJoinRequest> joinRequestRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Tournament> tournamentRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRealTimeNotifier realTimeNotifier,
        ITournamentLifecycleService lifecycleService,
        ISystemSettingsService systemSettingsService)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _playerRepository = playerRepository;
        _matchRepository = matchRepository;
        _joinRequestRepository = joinRequestRepository;
        _registrationRepository = registrationRepository;
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _realTimeNotifier = realTimeNotifier;
        _lifecycleService = lifecycleService;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<IEnumerable<TeamDto>> GetAllAsync()
    {
        var teams = await _teamRepository.GetAllAsync(t => t.Captain!, t => t.Players);
        var teamDtos = _mapper.Map<IEnumerable<TeamDto>>(teams).ToList();

        // Fetch all finished matches once to calculate stats for all teams efficiently
        var finishedMatches = (await _matchRepository.FindAsync(m => m.Status == Domain.Enums.MatchStatus.Finished)).ToList();

        foreach (var dto in teamDtos)
        {
            var stats = new TeamStatsDto();
            var teamMatches = finishedMatches.Where(m => m.HomeTeamId == dto.Id || m.AwayTeamId == dto.Id);

            foreach (var match in teamMatches)
            {
                stats.Matches++;
                bool isHome = match.HomeTeamId == dto.Id;
                int teamScore = isHome ? match.HomeScore : match.AwayScore;
                int opponentScore = isHome ? match.AwayScore : match.HomeScore;

                stats.GoalsFor += teamScore;
                stats.GoalsAgainst += opponentScore;

                if (teamScore > opponentScore) stats.Wins++;
                else if (teamScore == opponentScore) stats.Draws++;
                else stats.Losses++;
            }
            dto.Stats = stats;
        }

        return teamDtos;
    }

    // ... (Use existing methods until DisableTeamAsync)

    public async Task DisableTeamAsync(Guid teamId)
    {
        // 1. Get Team
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        // 2. Set Status to Disabled (Inactive)
        team.IsActive = false;
        await _teamRepository.UpdateAsync(team);

        await _analyticsService.LogActivityAsync("Team Disabled", $"Team {team.Name} (ID: {teamId}) has been disabled by Admin.", null, "Admin");

        // 3. Handle Active Tournaments (Withdrawal)
        var activeRegistrations = await _registrationRepository.FindAsync(r => r.TeamId == teamId && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.PendingPaymentReview));
        
        foreach (var reg in activeRegistrations)
        {
             // Use injected repository
             var tournament = await _tournamentRepository.GetByIdAsync(reg.TournamentId); 
             // Logic check? Assuming valid.
             
             // Mark as Withdrawn
             reg.Status = RegistrationStatus.Withdrawn;
             await _registrationRepository.UpdateAsync(reg);

             // 4. Forfeit Upcoming Matches
             var matches = await _matchRepository.FindAsync(m => m.TournamentId == reg.TournamentId && (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.Status == Domain.Enums.MatchStatus.Scheduled);
             
             foreach (var match in matches)
             {
                 match.Status = Domain.Enums.MatchStatus.Finished;
                 match.Forfeit = true;
                 
                 // Assign 3-0 loss
                 if (match.HomeTeamId == teamId)
                 {
                     match.HomeScore = 0;
                     match.AwayScore = 3; 
                 }
                 else
                 {
                     match.HomeScore = 3;
                     match.AwayScore = 0;
                 }
                 
                 await _matchRepository.UpdateAsync(match);
             }

             // 4.5 Check if this tournament should now be finalized
             await _lifecycleService.CheckAndFinalizeTournamentAsync(reg.TournamentId);
        }

        // 5. Notify Captain
        await _notificationService.SendNotificationAsync(team.CaptainId, "تم تعطيل الفريق", "تم تعطيل فريقك من قبل الإدارة وسحب جميع نتائجه القادمة.", "team_disabled");
    }

    public async Task<TeamDto?> GetByIdAsync(Guid id)
    {
        var team = await _teamRepository.GetByIdAsync(id, t => t.Captain!, t => t.Players);
        if (team == null) return null;

        var teamDto = _mapper.Map<TeamDto>(team);

        // Calculate stats from matches efficiently
        var finishedMatches = await _matchRepository.FindAsync(m => 
            (m.HomeTeamId == id || m.AwayTeamId == id) && 
            m.Status == Domain.Enums.MatchStatus.Finished);

        var stats = new TeamStatsDto();
        foreach (var match in finishedMatches)
        {
            stats.Matches++;
            bool isHome = match.HomeTeamId == id;
            int teamScore = isHome ? match.HomeScore : match.AwayScore;
            int opponentScore = isHome ? match.AwayScore : match.HomeScore;

            stats.GoalsFor += teamScore;
            stats.GoalsAgainst += opponentScore;

            if (teamScore > opponentScore) stats.Wins++;
            else if (teamScore == opponentScore) stats.Draws++;
            else stats.Losses++;
        }

        teamDto.Stats = stats;
        return teamDto;
    }

    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId)
    {
        // SYSTEM SETTING CHECK: Block team creation if disabled
        if (!await _systemSettingsService.IsTeamCreationAllowedAsync())
        {
            throw new BadRequestException("إنشاء الفرق متوقف حالياً");
        }

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
        
        if (request.IsActive.HasValue && team.IsActive != request.IsActive.Value)
        {
            team.IsActive = request.IsActive.Value;
            await _analyticsService.LogActivityAsync(
                team.IsActive ? "Team Activated" : "Team Deactivated",
                $"Team {team.Name} (ID: {id}) {(team.IsActive ? "activated" : "deactivated")} by system/admin.",
                null, "Admin");
        }

        await _teamRepository.UpdateAsync(team);
        return _mapper.Map<TeamDto>(team);
    }

    public async Task DeleteAsync(Guid id)
    {
        // 1. Validation: Removed check for matches to allow Soft Delete
        // The team and players will be soft-deleted, preserving match history references.


        // 2. Unlink Users (Set TeamId = null)
        var users = await _userRepository.FindAsync(u => u.TeamId == id);
        var memberUserIds = users.Select(u => u.Id).ToList();

        foreach (var user in users)
        {
            user.TeamId = null;
            await _userRepository.UpdateAsync(user);
        }

        // 3. Delete Players (Dependent Entity)
        // Teams always have at least one player (the captain) created on initialization
        var players = await _playerRepository.FindAsync(p => p.TeamId == id);
        foreach (var p in players)
        {
            await _playerRepository.DeleteAsync(p);
        }

        // 4. Delete Join Requests (Dependent Entity)
        var requests = await _joinRequestRepository.FindAsync(r => r.TeamId == id);
        foreach (var r in requests)
        {
            await _joinRequestRepository.DeleteAsync(r);
        }

        // Note: TeamRegistrations are not currently cleaned up. 
        // If a team is in a tournament, this might still fail (which is good safety).

        // 5. Delete Team
        await _teamRepository.DeleteAsync(id);

        // 6. Notify all members
        await _realTimeNotifier.SendTeamDeletedAsync(id, memberUserIds);
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
            Status = "pending",
            InitiatedByPlayer = true
        };

        await _joinRequestRepository.AddAsync(request);

        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team != null)
        {
            await _notificationService.SendNotificationAsync(team.CaptainId, "طلب انضمام جديد", $"يرغب اللاعب {user.Name} في الانضمام لفريقك.", "join_request");
        }

        var result = new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = playerId,
            PlayerName = user.Name,
            TeamId = teamId,
            TeamName = team?.Name ?? "Unknown",
            Status = "pending",
            RequestDate = request.CreatedAt,
            InitiatedByPlayer = true
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
                 RequestDate = r.CreatedAt,
                 InitiatedByPlayer = r.InitiatedByPlayer
             });
        }
        return dtos;
    }

    public async Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);

        User? user = null;
        if (approve)
        {
            request.Status = "approved";
             // Add player to team
             user = await _userRepository.GetByIdAsync(request.UserId);
             if (user != null) {
                 if (user.TeamId.HasValue) throw new ConflictException("هذا اللاعب مسجل بالفعل في فريق آخر.");
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

        var team = await _teamRepository.GetByIdAsync(teamId);
        user = await _userRepository.GetByIdAsync(request.UserId);

        // Notify player
        await _notificationService.SendNotificationAsync(request.UserId, 
            approve ? "تم قبول طلب الانضمام" : "تم رفض طلب الانضمام",
            approve ? $"لقد وافق فريق {team?.Name} على طلب انضمامك." : $"لقد رفض فريق {team?.Name} طلب انضمامك.",
            approve ? "join_accepted" : "join_rejected");

         // Helper to return DTO
         return new JoinRequestDto
         {
             Id = request.Id,
             PlayerId = request.UserId,
             PlayerName = user?.Name ?? "Unknown",
             TeamId = teamId,
             TeamName = team?.Name ?? "Unknown",
             Status = request.Status,
             RequestDate = request.CreatedAt,
             InitiatedByPlayer = request.InitiatedByPlayer
         };
    }

    public async Task<JoinRequestDto> InvitePlayerAsync(Guid teamId, Guid captainId, AddPlayerRequest request)
    {
        // 1. Verify Captain Ownership
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);
        if (team.CaptainId != captainId) throw new ForbiddenException("فقط قائد الفريق يمكنه إرسال دعوات.");

        // 2. Find User by DisplayId
        var users = await _userRepository.FindAsync(u => u.DisplayId == request.DisplayId);
        var user = users.FirstOrDefault();
        if (user == null) throw new NotFoundException("لم يتم العثور على لاعب بهذا الرقم التعريفي.");

        // 3. Validation
        if (user.TeamId.HasValue)
        {
            if (user.TeamId == teamId) throw new ConflictException("اللاعب مسجل بالفعل في هذا الفريق.");
            throw new ConflictException("هذا اللاعب مسجل بالفعل في فريق آخر.");
        }

        // Check for existing pending request
        var existingRequest = await _joinRequestRepository.FindAsync(r => r.TeamId == teamId && r.UserId == user.Id && r.Status == "pending");
        if (existingRequest.Any()) throw new ConflictException("تم إرسال دعوة بالفعل لهذا اللاعب.");

        // 4. Create Request
        var joinRequest = new TeamJoinRequest
        {
            TeamId = teamId,
            UserId = user.Id,
            Status = "pending",
            InitiatedByPlayer = false
        };
        await _joinRequestRepository.AddAsync(joinRequest);

        // 5. Notify Player
        await _notificationService.SendNotificationAsync(user.Id, "دعوة للانضمام لفريق", $"لقد دعاك فريق {team.Name} للانضمام إليه.", "invite");

        return new JoinRequestDto
        {
            Id = joinRequest.Id,
            PlayerId = user.Id,
            PlayerName = user.Name,
            Status = "pending",
            RequestDate = joinRequest.CreatedAt,
            InitiatedByPlayer = false
        };
    }

    public async Task<JoinRequestDto> AcceptInviteAsync(Guid requestId, Guid userId)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId, r => r.Team!);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);
        if (request.UserId != userId) throw new ForbiddenException("لا تملك صلاحية قبول هذه الدعوة.");
        if (request.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) throw new NotFoundException(nameof(User), userId);

        if (user.TeamId.HasValue) throw new ConflictException("أنت عضو في فريق آخر بالفعل.");

        // 1. Update Request
        request.Status = "approved";
        await _joinRequestRepository.UpdateAsync(request);

        // 2. Add to Team
        user.TeamId = request.TeamId;
        await _userRepository.UpdateAsync(user);

        var player = new Player
        {
            Name = user.Name,
            DisplayId = "P-" + user.DisplayId,
            UserId = user.Id,
            TeamId = request.TeamId
        };
        await _playerRepository.AddAsync(player);

        // 3. Notify Captain
        if (request.Team != null)
        {
            await _notificationService.SendNotificationAsync(request.Team.CaptainId, "تم قبول الدعوة", $"لقد قبل اللاعب {user.Name} دعوتك للانضمام للفريق.", "invite_accepted");
        }

        return new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = userId,
            PlayerName = user.Name,
            Status = "approved",
            RequestDate = request.CreatedAt
        };
    }

    public async Task<JoinRequestDto> RejectInviteAsync(Guid requestId, Guid userId)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId, r => r.Team!);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);
        if (request.UserId != userId) throw new ForbiddenException("لا تملك صلاحية رفض هذه الدعوة.");
        if (request.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        request.Status = "rejected";
        await _joinRequestRepository.UpdateAsync(request);

        // Notify Captain
        if (request.Team != null)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            await _notificationService.SendNotificationAsync(request.Team.CaptainId, "تم رفض الدعوة", $"لقد رفض اللاعب {user?.Name ?? "مجهول"} دعوتك للانضمام للفريق.", "invite_rejected");
        }

        return new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = userId,
            Status = "rejected",
            RequestDate = request.CreatedAt
        };
    }

    public async Task<IEnumerable<JoinRequestDto>> GetUserInvitationsAsync(Guid userId)
    {
        var requests = await _joinRequestRepository.FindAsync(r => r.UserId == userId && r.Status == "pending");
        var dtos = new List<JoinRequestDto>();
        foreach (var r in requests)
        {
            var team = await _teamRepository.GetByIdAsync(r.TeamId);
            dtos.Add(new JoinRequestDto
            {
                Id = r.Id,
                TeamId = r.TeamId,
                TeamName = team?.Name ?? "Unknown",
                PlayerId = userId,
                Status = r.Status,
                RequestDate = r.CreatedAt,
                InitiatedByPlayer = r.InitiatedByPlayer
            });
        }
        return dtos;
    }

    public async Task<IEnumerable<JoinRequestDto>> GetRequestsForCaptainAsync(Guid captainId)
    {
        var teams = await _teamRepository.FindAsync(t => t.CaptainId == captainId);
        var teamIds = teams.Select(t => t.Id).ToList();

        var requests = await _joinRequestRepository.FindAsync(r => teamIds.Contains(r.TeamId) && r.Status == "pending");
        var dtos = new List<JoinRequestDto>();
        foreach (var r in requests)
        {
            var user = await _userRepository.GetByIdAsync(r.UserId);
            var team = teams.FirstOrDefault(t => t.Id == r.TeamId);
            dtos.Add(new JoinRequestDto
            {
                Id = r.Id,
                TeamId = r.TeamId,
                TeamName = team?.Name ?? "Unknown",
                PlayerId = r.UserId,
                PlayerName = user?.Name ?? "Unknown",
                Status = r.Status,
                RequestDate = r.CreatedAt,
                InitiatedByPlayer = r.InitiatedByPlayer
            });
        }
        return dtos;
    }

    public async Task RemovePlayerAsync(Guid teamId, Guid playerId)
    {
        // playerId here is Player Entity Id? Yes from route {playerId} in team.
        var player = await _playerRepository.GetByIdAsync(playerId);
        // Safety check teamId
        if (player != null && player.TeamId == teamId)
        {
            // Rule: Captain cannot remove himself
            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team != null && player.UserId.HasValue && player.UserId.Value == team.CaptainId)
            {
                throw new ForbiddenException("لا يمكن للكابتن حذف نفسه من الفريق. استخدم خيار حذف الفريق بدلاً من ذلك.");
            }

            // Unlink User
            if (player.UserId.HasValue)
            {
                var userId = player.UserId.Value;
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    user.TeamId = null;
                    await _userRepository.UpdateAsync(user);

                    // Send real-time notification to the removed player
                    await _realTimeNotifier.SendRemovedFromTeamAsync(userId, teamId, playerId);
                }
            }

            await _playerRepository.DeleteAsync(player);
        }
    }

    public async Task<IEnumerable<PlayerDto>> GetTeamPlayersAsync(Guid teamId)
    {
        var players = await _playerRepository.FindAsync(p => p.TeamId == teamId);
        return _mapper.Map<IEnumerable<PlayerDto>>(players);
    }

    public async Task<IEnumerable<Application.DTOs.Matches.MatchDto>> GetTeamMatchesAsync(Guid teamId)
    {
        var matches = await _matchRepository.FindAsync(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);
        return _mapper.Map<IEnumerable<Application.DTOs.Matches.MatchDto>>(matches);
    }

    public async Task<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>> GetTeamFinancialsAsync(Guid teamId)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TeamId == teamId, new[] { "Tournament", "Team.Captain" });
        return _mapper.Map<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>>(registrations);
    }

}

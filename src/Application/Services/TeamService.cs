using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Teams;
using Application.DTOs.Users;
using Application.Interfaces;
using Application.Common;
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
    private readonly IMatchRepository _matchRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ITransactionManager _transactionManager;

    public TeamService(
        IRepository<Team> teamRepository,
        IRepository<User> userRepository,
        IRepository<Player> playerRepository,
        IMatchRepository matchRepository,
        IRepository<TeamJoinRequest> joinRequestRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Tournament> tournamentRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRealTimeNotifier realTimeNotifier,
        ITournamentLifecycleService lifecycleService,
        ISystemSettingsService systemSettingsService,
        ITransactionManager transactionManager)
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
        _transactionManager = transactionManager;
    }

    public async Task<Application.Common.Models.PagedResult<TeamDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? captainId = null, Guid? playerId = null, CancellationToken ct = default)
    {
        System.Linq.Expressions.Expression<Func<Team, bool>>? predicate = null;
        var includes = new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players, t => t.Statistics! };

        if (captainId.HasValue)
        {
            predicate = t => t.Players.Any(p => p.TeamRole == TeamRole.Captain && p.UserId == captainId.Value);
        }
        else if (playerId.HasValue)
        {
            predicate = t => t.Players.Any(p => p.UserId == playerId.Value);
        }

        var result = await _teamRepository.GetPagedAsync(pageNumber, pageSize, predicate, q => q.OrderBy(t => t.Name), ct, includes);
        var teamDtos = _mapper.Map<List<TeamDto>>(result.Items);
        
        return new Application.Common.Models.PagedResult<TeamDto>(teamDtos, result.TotalCount, pageNumber, pageSize);
    }

    public async Task<TeamDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var team = await _teamRepository.GetByIdNoTrackingAsync(id, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players, t => t.Statistics! }, ct);
        if (team == null) return null;

        return _mapper.Map<TeamDto>(team);
    }

    // ... (Use existing methods until DisableTeamAsync)

    public async Task DisableTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        // 1. Get Team
        var team = await _teamRepository.GetByIdAsync(teamId, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        // 2. Set Status to Disabled (Inactive)
        team.IsActive = false;
        await _teamRepository.UpdateAsync(team, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            "TEAM_DISABLED", // Assuming this code is acceptable or needs adding to constants
            new Dictionary<string, string> { { "teamName", team.Name } }, 
            null, 
            "إدارة"
        , ct);

        // 3. Handle Active Tournaments (Withdrawal)
        var activeRegistrations = await _registrationRepository.FindAsync(r => r.TeamId == teamId && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.PendingPaymentReview), ct);
        
        if (activeRegistrations.Any())
        {
            foreach (var reg in activeRegistrations)
            {
                reg.Status = RegistrationStatus.Withdrawn;
            }
            await _registrationRepository.UpdateRangeAsync(activeRegistrations, ct);

            foreach (var reg in activeRegistrations)
            {
                // Forfeit Upcoming Matches
                var matches = await _matchRepository.FindAsync(m => m.TournamentId == reg.TournamentId && (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.Status == Domain.Enums.MatchStatus.Scheduled, ct);
                
                foreach (var match in matches)
                {
                    match.Status = Domain.Enums.MatchStatus.Finished;
                    match.Forfeit = true;
                    
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
                }
                await _matchRepository.UpdateRangeAsync(matches, ct);

                // Check if this tournament should now be finalized
                await _lifecycleService.CheckAndFinalizeTournamentAsync(reg.TournamentId, ct);
            }
        }

        // 5. Notify Captain
        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain != null && captain.UserId.HasValue)
        {
            await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.ACCOUNT_SUSPENDED, entityId: teamId, entityType: "team", ct: ct);
        }
    }

    public async Task ActivateTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var team = await _teamRepository.GetByIdAsync(teamId, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        team.IsActive = true;
        await _teamRepository.UpdateAsync(team, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            "TEAM_ACTIVATED",
            new Dictionary<string, string> { { "teamName", team.Name } },
            null,
            "إدارة"
        , ct);

        // Notify Captain
        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain != null && captain.UserId.HasValue)
        {
            await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.TEAM_ACTIVATED, entityId: teamId, entityType: "team", ct: ct);
        }
    }


    private async Task ValidateManagementRights(Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        if (userRole == UserRole.Admin.ToString()) return;

        var team = await _teamRepository.GetByIdNoTrackingAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        var isCaptain = team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain);
        if (!isCaptain)
        {
            throw new ForbiddenException("غير مصرح لك بإدارة هذا الفريق. فقط قائد الفريق أو مدير النظام يمكنه ذلك.");
        }
    }

    public async Task<TeamDto> CreateAsync(CreateTeamRequest request, Guid captainId, CancellationToken ct = default)
    {
        // SYSTEM SETTING CHECK: Block team creation if disabled
        if (!await _systemSettingsService.IsTeamCreationAllowedAsync(ct))
        {
            throw new BadRequestException("إنشاء الفرق متوقف حالياً");
        }

        var captain = await _userRepository.GetByIdAsync(captainId, ct);
        if (captain == null) throw new NotFoundException(nameof(User), captainId);

        // Allow users to create multiple teams - remove single-team validation

        var team = new Team
        {
            Name = request.Name,
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
            UserId = captainId,
            TeamRole = TeamRole.Captain
        };
        team.Players.Add(player);

        // Save team (will save players too due to relationship)
        await _teamRepository.AddAsync(team, ct);

        // link user to team if they don't have one yet
        if (!captain.TeamId.HasValue)
        {
            captain.TeamId = team.Id;
            await _userRepository.UpdateAsync(captain, ct);
        }

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TEAM_CREATED, 
            new Dictionary<string, string> { { "teamName", team.Name } }, 
            captainId, 
            captain.Name
        , ct);
        
        var dto = _mapper.Map<TeamDto>(team);
        dto.CaptainName = captain.Name; // Ensure it's populated for the immediate response
        
        await _realTimeNotifier.SendTeamCreatedAsync(dto, ct);
        
        return dto;
    }

    public async Task<TeamDto> UpdateAsync(Guid id, UpdateTeamRequest request, Guid userId, string userRole, CancellationToken ct = default)
    {
        await ValidateManagementRights(id, userId, userRole, ct);
        var team = await _teamRepository.GetByIdAsync(id, ct);
        if (team == null) throw new NotFoundException(nameof(Team), id);

        if (!string.IsNullOrEmpty(request.Name)) team.Name = request.Name!;
        if (!string.IsNullOrEmpty(request.City)) team.City = request.City;
        if (!string.IsNullOrEmpty(request.Logo)) team.Logo = request.Logo;
        
        if (request.IsActive.HasValue && team.IsActive != request.IsActive.Value)
        {
            team.IsActive = request.IsActive.Value;
            await _analyticsService.LogActivityByTemplateAsync(
                team.IsActive ? "TEAM_ACTIVATED" : "TEAM_DEACTIVATED",
                new Dictionary<string, string> { { "teamName", team.Name } },
                null, "إدارة", ct);
        }

        await _teamRepository.UpdateAsync(team, ct);
        var dto = _mapper.Map<TeamDto>(team);
        await _realTimeNotifier.SendTeamUpdatedAsync(dto, ct);
        return dto;
    }

    public async Task DeleteAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        await ValidateManagementRights(id, userId, userRole, ct);

        // PERF-FIX D9: Use ITransactionManager with retry strategy instead of raw Begin/Commit/Rollback
        var (memberUserIds, affectedTournamentIds) = await _transactionManager.ExecuteInTransactionAsync(async () =>
        {
            // 2. Unlink Users (Set TeamId = null)
            var users = await _userRepository.FindAsync(u => u.TeamId == id, ct);
            var memberIds = users.Select(u => u.Id).ToList();

            if (users.Any())
            {
                foreach (var user in users)
                {
                    user.TeamId = null;
                }
                await _userRepository.UpdateRangeAsync(users, ct);
                
                foreach (var user in users)
                {
                    await _realTimeNotifier.SendUserUpdatedAsync(_mapper.Map<UserDto>(user));
                }
            }

            // 3. Delete Players (Dependent Entity)
            var players = await _playerRepository.FindAsync(p => p.TeamId == id, ct);
            await _playerRepository.DeleteRangeAsync(players, ct);

            // 4. Delete Join Requests (Dependent Entity)
            var requests = await _joinRequestRepository.FindAsync(r => r.TeamId == id, ct);
            await _joinRequestRepository.DeleteRangeAsync(requests, ct);

            // 5. Delete TeamRegistrations and collect affected tournaments
            var registrations = await _registrationRepository.FindAsync(r => r.TeamId == id, ct);
            var tournamentIds = registrations.Select(r => r.TournamentId).Distinct().ToList();

            if (registrations.Any())
            {
                var approvedOrPending = registrations.Where(r => r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.PendingPaymentReview).ToList();
                if (approvedOrPending.Any())
                {
                    var tIds = approvedOrPending.Select(r => r.TournamentId).Distinct().ToList();
                    var tournaments = await _tournamentRepository.FindAsync(t => tIds.Contains(t.Id), ct);
                    foreach (var tournament in tournaments)
                    {
                        var affectedCount = approvedOrPending.Count(r => r.TournamentId == tournament.Id);
                        tournament.CurrentTeams = Math.Max(0, tournament.CurrentTeams - affectedCount);
                    }
                    await _tournamentRepository.UpdateRangeAsync(tournaments, ct);
                }
                await _registrationRepository.DeleteRangeAsync(registrations, ct);
            }

            // 6. Delete Team
            await _teamRepository.DeleteAsync(id, ct);

            return (memberIds, tournamentIds);
        }, ct);

        // 7. Notify all members (Specific) AND Global List (General)
        await _realTimeNotifier.SendTeamDeletedAsync(id, memberUserIds, ct);
        await _realTimeNotifier.SendTeamDeletedAsync(id, ct);

        // 8. Emit TournamentUpdated for each affected tournament
        foreach (var tournamentId in affectedTournamentIds)
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
            if (tournament != null)
            {
                var dto = _mapper.Map<Application.DTOs.Tournaments.TournamentDto>(tournament);
                await _realTimeNotifier.SendTournamentUpdatedAsync(dto, ct);
            }
        }
    }

    public async Task<JoinRequestDto> RequestJoinAsync(Guid teamId, Guid playerId, CancellationToken ct = default)
    {
        // playerId here likely refers to User.Id (Candidate)
        var existingRequest = await _joinRequestRepository.FindAsync(r => r.TeamId == teamId && r.UserId == playerId && r.Status == "pending", ct);
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

        var user = await _userRepository.GetByIdAsync(playerId, ct);
        if (user == null) throw new NotFoundException(nameof(User), playerId);

        var request = new TeamJoinRequest
        {
            TeamId = teamId,
            UserId = playerId,
            Status = "pending",
            InitiatedByPlayer = true
        };

        await _joinRequestRepository.AddAsync(request, ct);

        var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team != null)
        {
            var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.JOIN_REQUEST_RECEIVED, new Dictionary<string, string> { { "playerName", user.Name } }, entityId: teamId, entityType: "team", ct: ct);
            }
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

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TEAM_JOINED, // Using JOINED for request for now, or add TEAM_JOIN_REQUEST
            new Dictionary<string, string> { 
                { "playerName", user.Name },
                { "teamName", team?.Name ?? "الفريق" }
            }, 
            playerId, 
            user.Name
        , ct);
        return result;
    }

    public async Task<Application.Common.Models.PagedResult<JoinRequestDto>> GetJoinRequestsAsync(Guid teamId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            page,
            pageSize,
            r => r.TeamId == teamId,
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.User!
        );
        
        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id,
            PlayerId = r.UserId,
            PlayerName = r.User?.Name ?? "Unknown",
            Status = r.Status,
            RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new Application.Common.Models.PagedResult<JoinRequestDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<JoinRequestDto> RespondJoinRequestAsync(Guid teamId, Guid requestId, bool approve, Guid userId, string userRole, CancellationToken ct = default)
    {
        await ValidateManagementRights(teamId, userId, userRole, ct);
        var request = await _joinRequestRepository.GetByIdAsync(requestId, ct);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);

        User? user = null;
        if (approve)
        {
            request.Status = "approved";
             // Add player to team
             user = await _userRepository.GetByIdAsync(request.UserId, ct);
             if (user != null) {
                 if (!user.TeamId.HasValue) user.TeamId = teamId;
                 await _userRepository.UpdateAsync(user, ct);
                 await _realTimeNotifier.SendUserUpdatedAsync(_mapper.Map<UserDto>(user));

                 var player = new Player
                 {
                     Name = user.Name,
                     DisplayId = "P-" + user.DisplayId,
                     UserId = user.Id,
                     TeamId = teamId,
                     TeamRole = TeamRole.Member
                 };
                 await _playerRepository.AddAsync(player, ct);
             }
        }
        else
        {
            request.Status = "rejected";
        }

        await _joinRequestRepository.UpdateAsync(request, ct);
        if (approve)
        {
            await BroadcastTeamSnapshotAsync(teamId, ct);
        }

        var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        user = await _userRepository.GetByIdAsync(request.UserId, ct);

        // Notify player
        await _notificationService.SendNotificationByTemplateAsync(request.UserId, 
            approve ? NotificationTemplates.PLAYER_JOINED_TEAM : NotificationTemplates.JOIN_REQUEST_REJECTED,
            new Dictionary<string, string> { { "teamName", team?.Name ?? "الفريق" } },
            entityId: teamId, entityType: "team", ct: ct);

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

    public async Task<JoinRequestDto> InvitePlayerAsync(Guid teamId, Guid captainId, AddPlayerRequest request, CancellationToken ct = default)
    {
        // 1. Verify Captain Ownership
        var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);
        
        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain == null || captain.UserId != captainId) throw new ForbiddenException("فقط قائد الفريق يمكنه إرسال دعوات.");

        // 2. Find User by DisplayId
        var users = await _userRepository.FindAsync(u => u.DisplayId == request.DisplayId, ct);
        var user = users.FirstOrDefault();
        if (user == null) throw new NotFoundException("لم يتم العثور على لاعب بهذا الرقم التعريفي.");

        // 3. Validation
        if (user.TeamId == teamId || team.Players.Any(p => p.UserId == user.Id))
        {
            throw new ConflictException("اللاعب مسجل بالفعل في هذا الفريق.");
        }

        // Check for existing pending request
        var existingRequest = await _joinRequestRepository.FindAsync(r => r.TeamId == teamId && r.UserId == user.Id && r.Status == "pending", ct);
        if (existingRequest.Any()) throw new ConflictException("تم إرسال دعوة بالفعل لهذا اللاعب.");

        // 4. Create Request
        var joinRequest = new TeamJoinRequest
        {
            TeamId = teamId,
            UserId = user.Id,
            Status = "pending",
            InitiatedByPlayer = false
        };
        await _joinRequestRepository.AddAsync(joinRequest, ct);

        // 5. Notify Player
        await _notificationService.SendNotificationByTemplateAsync(user.Id, NotificationTemplates.INVITE_RECEIVED, new Dictionary<string, string> { { "teamName", team.Name } }, entityId: team.Id, entityType: "team", ct: ct);

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

    public async Task<PlayerDto> AddGuestPlayerAsync(Guid teamId, Guid captainId, AddGuestPlayerRequest request, CancellationToken ct = default)
    {
        // 1. Validate
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("اسم اللاعب مطلوب.");

        // 2. Verify Captain Ownership
        var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain == null || captain.UserId != captainId) throw new ForbiddenException("فقط قائد الفريق يمكنه إضافة لاعبين.");

        // 3. Check duplicate name in same team
        if (team.Players.Any(p => p.Name.Trim().Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("يوجد لاعب بنفس الاسم في الفريق بالفعل.");
        }

        // 4. Create Player (no UserId - guest player)
        var player = new Player
        {
            Name = request.Name.Trim(),
            DisplayId = "G-" + new Random().Next(100000, 999999),
            UserId = null, // Guest player - no account
            TeamId = teamId,
            TeamRole = TeamRole.Member,
            Number = request.Number ?? 0,
            Position = request.Position ?? "لاعب",
            Status = "active"
        };
        await _playerRepository.AddAsync(player, ct);

        // 5. Broadcast update
        await BroadcastTeamSnapshotAsync(teamId, ct);

        // 6. Log activity
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TEAM_JOINED,
            new Dictionary<string, string> {
                { "playerName", player.Name },
                { "teamName", team.Name }
            },
            captainId,
            captain.Name ?? "الكابتن"
        , ct);

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<JoinRequestDto> AcceptInviteAsync(Guid requestId, Guid userId, CancellationToken ct = default)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId, new[] { "Team.Players" }, ct);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);
        if (request.UserId != userId) throw new ForbiddenException("لا تملك صلاحية قبول هذه الدعوة.");
        if (request.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null) throw new NotFoundException(nameof(User), userId);

        // Allow joining multiple teams - remove single-team blocking check
        // if (user.TeamId.HasValue) throw new ConflictException("أنت عضو في فريق آخر بالفعل.");

        // 1. Update Request
        request.Status = "approved";
        await _joinRequestRepository.UpdateAsync(request, ct);

        // 2. Add to Team
        if (!user.TeamId.HasValue) user.TeamId = request.TeamId;
        await _userRepository.UpdateAsync(user, ct);
        await _realTimeNotifier.SendUserUpdatedAsync(_mapper.Map<UserDto>(user));

        var player = new Player
        {
            Name = user.Name,
            DisplayId = "P-" + user.DisplayId,
            UserId = user.Id,
            TeamId = request.TeamId,
            TeamRole = TeamRole.Member
        };
        await _playerRepository.AddAsync(player, ct);
        await BroadcastTeamSnapshotAsync(request.TeamId, ct);

        // 3. Notify Captain
        if (request.Team != null)
        {
            var captain = request.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.INVITE_ACCEPTED, new Dictionary<string, string> { { "playerName", user.Name } }, ct: ct);
            }
        }

        return new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = userId,
            PlayerName = user.Name,
            TeamId = request.TeamId,
            TeamName = request.Team?.Name ?? "Unknown",
            Status = "approved",
            RequestDate = request.CreatedAt
        };
    }

    public async Task<JoinRequestDto> RejectInviteAsync(Guid requestId, Guid userId, CancellationToken ct = default)
    {
        var request = await _joinRequestRepository.GetByIdAsync(requestId, new[] { "Team.Players" }, ct);
        if (request == null) throw new NotFoundException(nameof(TeamJoinRequest), requestId);
        if (request.UserId != userId) throw new ForbiddenException("لا تملك صلاحية رفض هذه الدعوة.");
        if (request.Status != "pending") throw new ConflictException("هذه الدعوة لم تعد صالحة.");

        request.Status = "rejected";
        await _joinRequestRepository.UpdateAsync(request, ct);

        // Notify Captain
        if (request.Team != null)
        {
            var captain = request.Team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(userId, ct);
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.INVITE_REJECTED, new Dictionary<string, string> { { "playerName", user?.Name ?? "اللاعب" } }, ct: ct);
            }
        }

        return new JoinRequestDto
        {
            Id = request.Id,
            PlayerId = userId,
            Status = "rejected",
            RequestDate = request.CreatedAt
        };
    }

    public async Task<Application.Common.Models.PagedResult<JoinRequestDto>> GetUserInvitationsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            page,
            pageSize,
            r => r.UserId == userId && r.Status == "pending",
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.Team!
        );

        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id,
            TeamId = r.TeamId,
            TeamName = r.Team?.Name ?? "Unknown",
            PlayerId = userId,
            Status = r.Status,
            RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new Application.Common.Models.PagedResult<JoinRequestDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Application.Common.Models.PagedResult<JoinRequestDto>> GetRequestsForCaptainAsync(Guid captainId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var teams = await _teamRepository.FindAsync(t => t.Players.Any(p => p.TeamRole == TeamRole.Captain && p.UserId == captainId), new[] { "Players" }, ct);
        var teamIds = teams.Select(t => t.Id).ToList();

        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            page,
            pageSize,
            r => teamIds.Contains(r.TeamId) && r.Status == "pending",
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.User!
        );
        
        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id,
            TeamId = r.TeamId,
            TeamName = teams.FirstOrDefault(t => t.Id == r.TeamId)?.Name ?? "Unknown",
            PlayerId = r.UserId,
            PlayerName = r.User?.Name ?? "Unknown",
            Status = r.Status,
            RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new Application.Common.Models.PagedResult<JoinRequestDto>(dtos, totalCount, page, pageSize);
    }

    public async Task RemovePlayerAsync(Guid teamId, Guid playerId, Guid userId, string userRole, CancellationToken ct = default)
    {
        await ValidateManagementRights(teamId, userId, userRole, ct);
        // playerId here is Player Entity Id? Yes from route {playerId} in team.
        var player = await _playerRepository.GetByIdAsync(playerId, ct);
        // Safety check teamId
        if (player != null && player.TeamId == teamId)
        {
            // Rule: Captain cannot remove himself
            var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
            var captain = team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (team != null && player.UserId.HasValue && captain != null && player.UserId.Value == captain.UserId)
            {
                throw new ForbiddenException("لا يمكن للكابتن حذف نفسه من الفريق. استخدم خيار حذف الفريق بدلاً من ذلك.");
            }

            // Unlink User
            if (player.UserId.HasValue)
            {
                var targetUserId = player.UserId.Value;
                var user = await _userRepository.GetByIdAsync(targetUserId, ct);
                if (user != null)
                {
                    if (user.TeamId == teamId) 
                    {
                        // Find another team they are a member of to set as primary, or null if none
                        var otherTeams = await _playerRepository.FindAsync(p => p.UserId == targetUserId && p.TeamId != teamId, ct);
                        user.TeamId = otherTeams.FirstOrDefault()?.TeamId;
                    }
                    await _userRepository.UpdateAsync(user, ct);
                    await _realTimeNotifier.SendUserUpdatedAsync(_mapper.Map<UserDto>(user));

                    // Send real-time notification to the removed player
                    await _realTimeNotifier.SendRemovedFromTeamAsync(targetUserId, teamId, playerId, ct);
                    
                    // Persistent Notification
                    await _notificationService.SendNotificationByTemplateAsync(targetUserId, NotificationTemplates.PLAYER_REMOVED, new Dictionary<string, string> { { "teamName", team?.Name ?? "الفريق" } }, entityId: teamId, entityType: "team", ct: ct);
                }
            }

            await _playerRepository.DeleteAsync(player, ct);
            await BroadcastTeamSnapshotAsync(teamId, ct);
        }
    }

    private async Task BroadcastTeamSnapshotAsync(Guid teamId, CancellationToken ct = default)
    {
        var teamDto = await GetByIdAsync(teamId, ct);
        if (teamDto != null)
        {
            await _realTimeNotifier.SendTeamUpdatedAsync(teamDto, ct);
        }
    }

    public async Task<Application.Common.Models.PagedResult<PlayerDto>> GetTeamPlayersAsync(Guid teamId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _playerRepository.GetPagedAsync(
            page,
            pageSize,
            p => p.TeamId == teamId,
            q => q.OrderBy(p => p.Name),
            ct
        );
        var dtos = _mapper.Map<List<PlayerDto>>(items);
        return new Application.Common.Models.PagedResult<PlayerDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Application.Common.Models.PagedResult<Application.DTOs.Matches.MatchDto>> GetTeamMatchesAsync(Guid teamId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _matchRepository.GetPagedAsync(
            page,
            pageSize,
            m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId),
            q => q.OrderByDescending(m => m.Date),
            ct
        );
        var dtos = _mapper.Map<List<Application.DTOs.Matches.MatchDto>>(items);
        return new Application.Common.Models.PagedResult<Application.DTOs.Matches.MatchDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<Application.Common.Models.PagedResult<Application.DTOs.Tournaments.TeamRegistrationDto>> GetTeamFinancialsAsync(Guid teamId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _registrationRepository.GetPagedAsync(
            page,
            pageSize,
            r => r.TeamId == teamId,
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.Tournament!,
            r => r.Team!.Players
        );
        var dtos = _mapper.Map<List<Application.DTOs.Tournaments.TeamRegistrationDto>>(items);
        return new Application.Common.Models.PagedResult<Application.DTOs.Tournaments.TeamRegistrationDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<TeamsOverviewDto> GetTeamsOverviewAsync(Guid userId, CancellationToken ct = default)
    {
        // Get teams owned by user (where user is captain)
        var ownedTeams = await _teamRepository.FindAsync(t => t.Players.Any(p => p.TeamRole == TeamRole.Captain && p.UserId == userId), new[] { "Players", "Statistics" }, ct);
        
        // Get teams where user is a member (through Player records)
        var playerTeams = await _teamRepository.FindAsync(
            t => t.Players.Any(p => p.UserId == userId), 
            new[] { "Players", "Statistics" }, ct 
        );
        
        // Get pending invitations for user
        var pendingInvitations = await _joinRequestRepository.FindAsync(
            r => r.UserId == userId && r.Status == "pending",
            new[] { "Team" }
        , ct);
        
        // Convert to DTOs
        var ownedTeamsDtos = _mapper.Map<List<TeamDto>>(ownedTeams);
        var memberTeamsDtos = _mapper.Map<List<TeamDto>>(playerTeams);
        
        // Remove duplicates - teams where user is both owner and member should only appear in ownedTeams
        var ownedTeamIds = ownedTeamsDtos.Select(t => t.Id).ToHashSet();
        memberTeamsDtos = memberTeamsDtos.Where(t => !ownedTeamIds.Contains(t.Id)).ToList();
        
        // Map join requests to DTOs
        var invitationDtos = pendingInvitations.Select(r => new JoinRequestDto
        {
            Id = r.Id,
            TeamId = r.TeamId,
            TeamName = r.Team?.Name ?? "",
            PlayerId = r.UserId,
            PlayerName = r.User?.Name ?? "",
            RequestDate = r.CreatedAt,
            Status = r.Status,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();
        

        
        return new TeamsOverviewDto
        {
            OwnedTeams = ownedTeamsDtos,
            MemberTeams = memberTeamsDtos,
            PendingInvitations = invitationDtos
        };
    }
}

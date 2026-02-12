using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Services;

public class TournamentService : ITournamentService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRealTimeNotifier _notifier;

    public TournamentService(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRepository<Team> teamRepository,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _teamRepository = teamRepository;
        _notifier = notifier;
    }

    public async Task<IEnumerable<TournamentDto>> GetAllAsync(Guid? creatorId = null)
    {
        IEnumerable<Tournament> tournaments;
        var includes = new[] { "Registrations", "Registrations.Team", "Registrations.Team.Players", "WinnerTeam" };
        
        if (creatorId.HasValue)
        {
            tournaments = await _tournamentRepository.GetNoTrackingAsync(
                t => t.CreatorUserId == creatorId.Value,
                includes
            );
        }
        else
        {
            tournaments = await _tournamentRepository.GetAllNoTrackingAsync(includes);
        }

        var tournamentList = tournaments.ToList();
        var ids = tournamentList.Select(t => t.Id).ToList();

        // Fix N+1: Pre-fetch all matches and registrations for checking intervention
        var allMatches = (await _matchRepository.GetNoTrackingAsync(m => ids.Contains(m.TournamentId)))
            .GroupBy(m => m.TournamentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allRegistrations = (await _registrationRepository.GetNoTrackingAsync(r => ids.Contains(r.TournamentId)))
            .GroupBy(r => r.TournamentId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var dtos = new List<TournamentDto>();
        foreach (var t in tournamentList)
        {
            var matches = allMatches.GetValueOrDefault(t.Id, new List<Match>());
            var registrations = allRegistrations.GetValueOrDefault(t.Id, new List<TeamRegistration>());
            
            var dto = _mapper.Map<TournamentDto>(t);
            dto.RequiresAdminIntervention = CheckInterventionRequiredInternal(t, matches, registrations);
            dtos.Add(dto);
        }
        return dtos;
    }

    public async Task<TournamentDto?> GetByIdAsync(Guid id)
    {
        var tournament = await _tournamentRepository.GetByIdNoTrackingAsync(id, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Players", "WinnerTeam" });
        if (tournament == null) return null;

        var matches = (await _matchRepository.GetNoTrackingAsync(m => m.TournamentId == id)).ToList();
        var registrations = (await _registrationRepository.GetNoTrackingAsync(r => r.TournamentId == id)).ToList();

        var dto = _mapper.Map<TournamentDto>(tournament);
        dto.RequiresAdminIntervention = CheckInterventionRequiredInternal(tournament, matches, registrations);
        return dto;
    }

    private async Task<TournamentDto?> GetByIdFreshAsync(Guid id)
    {
        return await GetByIdAsync(id);
    }

    private bool CheckInterventionRequiredInternal(Tournament tournament, List<Match> matches, List<TeamRegistration> registrations)
    {
        var relevantRegistrations = registrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        var regCount = relevantRegistrations.Count;
        var approvedCount = relevantRegistrations.Count(r => r.Status == RegistrationStatus.Approved);

        // Case 1: Registration closed but should be active (All approved, capacity reached, but no matches)
        if (tournament.Status == "registration_closed")
        {
            if (regCount == tournament.MaxTeams && approvedCount == regCount && !matches.Any())
            {
                 return true;
            }
            
            // Deadline passed but no matches generated
            if (DateTime.UtcNow > tournament.StartDate && !matches.Any())
            {
                return true;
            }
        }

        // Case 2: Active but stuck?
        if (tournament.Status == "active")
        {
            // No matches at all but active
            if (!matches.Any()) return true;

            // All matches finished but status is not completed
            if (matches.All(m => m.Status == MatchStatus.Finished)) return true;

            // Past end date but not completed
            if (DateTime.UtcNow > tournament.EndDate.AddDays(2)) return true;
        }

        // Case 3: Completed but missing winner?
        if (tournament.Status == "completed" && tournament.WinnerTeamId == null && matches.Any())
        {
            return true;
        }

        return false;
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid? creatorId = null)
    {
        var tournament = new Tournament
        {
            Name = request.Name,
            Description = request.Description,
            CreatorUserId = creatorId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            EntryFee = request.EntryFee,
            MaxTeams = request.MaxTeams,
            Location = request.Location,
            Rules = request.Rules,
            Prizes = request.Prizes,
            Status = "registration_open",
            Format = request.Format,
            MatchType = request.MatchType,
            NumberOfGroups = request.NumberOfGroups,
            QualifiedTeamsPerGroup = request.QualifiedTeamsPerGroup,
            WalletNumber = request.WalletNumber,
            InstaPayNumber = request.InstaPayNumber,
            IsHomeAwayEnabled = request.IsHomeAwayEnabled,
            SeedingMode = request.SeedingMode,
            PaymentMethodsJson = request.PaymentMethodsJson
        };

        await _tournamentRepository.AddAsync(tournament);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TOURNAMENT_CREATED, 
            new Dictionary<string, string> { { "tournamentName", tournament.Name } }, 
            null, 
            "آدمن"
        );
        
        // Real-time Event
        await _notifier.SendTournamentCreatedAsync(_mapper.Map<TournamentDto>(tournament));
        
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Players", "WinnerTeam" });
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);
        ValidateOwnership(tournament, userId, userRole);

        // Safety Check: Format Change
        if (request.Format.HasValue && request.Format.Value != tournament.Format)
        {
            var matches = await _matchRepository.FindAsync(m => m.TournamentId == id);
            // If any knockout match exists (GroupId == null AND Stage != League) and is started/finished, block change
            var hasActiveKnockout = matches.Any(m => m.GroupId == null && m.StageName != "League" && m.Status != MatchStatus.Scheduled);
            
            if (hasActiveKnockout)
            {
                throw new ConflictException("لا يمكن تغيير نظام البطولة بعد بدء الأدوار الإقصائية.");
            }
        }

        if (request.Name != null) tournament.Name = request.Name;
        if (request.Description != null) tournament.Description = request.Description;
        if (request.Status != null) tournament.Status = request.Status;
        if (request.StartDate.HasValue) tournament.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) tournament.EndDate = request.EndDate.Value;
        if (request.RegistrationDeadline.HasValue) tournament.RegistrationDeadline = request.RegistrationDeadline.Value;
        if (request.EntryFee.HasValue) tournament.EntryFee = request.EntryFee.Value;
        if (request.MaxTeams.HasValue) tournament.MaxTeams = request.MaxTeams.Value;
        if (request.Location != null) tournament.Location = request.Location;
        if (request.Rules != null) tournament.Rules = request.Rules;
        if (request.Prizes != null) tournament.Prizes = request.Prizes;
        if (request.Format.HasValue) tournament.Format = request.Format.Value;
        if (request.MatchType.HasValue) tournament.MatchType = request.MatchType.Value;
        if (request.NumberOfGroups.HasValue) tournament.NumberOfGroups = request.NumberOfGroups.Value;
        if (request.QualifiedTeamsPerGroup.HasValue) tournament.QualifiedTeamsPerGroup = request.QualifiedTeamsPerGroup.Value;
        if (request.WalletNumber != null) tournament.WalletNumber = request.WalletNumber;
        if (request.InstaPayNumber != null) tournament.InstaPayNumber = request.InstaPayNumber;
        if (request.IsHomeAwayEnabled.HasValue) tournament.IsHomeAwayEnabled = request.IsHomeAwayEnabled.Value;
        if (request.SeedingMode.HasValue) tournament.SeedingMode = request.SeedingMode.Value;
        if (request.PaymentMethodsJson != null) tournament.PaymentMethodsJson = request.PaymentMethodsJson;

        // Force auto-closure if capacity reached after manual update
        if (tournament.CurrentTeams >= tournament.MaxTeams && tournament.Status == "registration_open")
        {
            tournament.Status = "registration_closed";
        }
        else if (tournament.CurrentTeams < tournament.MaxTeams && tournament.Status == "registration_closed" && !(await _matchRepository.FindAsync(m => m.TournamentId == id)).Any())
        {
            // Re-open if capacity was increased and no matches exist
            if (DateTime.UtcNow <= tournament.RegistrationDeadline)
            {
                tournament.Status = "registration_open";
            }
        }

        await _tournamentRepository.UpdateAsync(tournament);

        var dto = await GetByIdFreshAsync(id);

        // Real-time Event
        if (dto != null)
        {
            await _notifier.SendTournamentUpdatedAsync(dto);
        }

        return dto ?? _mapper.Map<TournamentDto>(tournament);
    }

    public async Task DeleteAsync(Guid id, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole);

        // Delete associated matches and registrations logic normally handled by DB constraints or repository
        // For now, assuming direct delete is safe or handled
        await _tournamentRepository.DeleteAsync(tournament);
    }

    public async Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations" });
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        
        if (DateTime.UtcNow > tournament.RegistrationDeadline)
        {
            throw new ConflictException("انتهى موعد التسجيل في البطولة.");
        }

        if (tournament.Registrations.Count >= tournament.MaxTeams)
        {
            throw new ConflictException("اكتمل عدد الفرق في البطولة.");
        }

        var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" });
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);
        
        // SECURITY CHECK: Verify that the current user is actually the captain
        if (!team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain))
        {
             throw new ForbiddenException("فقط كابتن الفريق يمكنه تسجيل الفريق في البطولات.");
        }

        // Check if ALREADY registered or PENDING
        if (tournament.Registrations.Any(r => r.TeamId == request.TeamId && r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn))
        {
             throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة أو قيد المراجعة.");
        }

        var registration = new TeamRegistration
        {
            TournamentId = tournamentId,
            TeamId = request.TeamId,
            // RegisteredAt handled by BaseEntity.CreatedAt
            Status = RegistrationStatus.PendingPaymentReview 
        };
        
        // A team is considered "IN the tournament" once they apply (Approved + Pending count towards capacity)
        tournament.CurrentTeams++;

        // If free tournament
        if (tournament.EntryFee <= 0)
        {
            registration.Status = RegistrationStatus.Approved;
        }

        // Auto-close registration if capacity reached
        if (tournament.CurrentTeams >= tournament.MaxTeams)
        {
            tournament.Status = "registration_closed";
        }

        await _registrationRepository.AddAsync(registration);
        await _tournamentRepository.UpdateAsync(tournament);
        
        var registrationDto = _mapper.Map<TeamRegistrationDto>(registration);
        
        // Notify Real-Time
        var tournamentDto = await GetByIdFreshAsync(tournamentId);
        if (tournamentDto != null)
        {
            await _notifier.SendTournamentUpdatedAsync(tournamentDto);
        }

        return registrationDto;
    }

    public async Task<IEnumerable<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId)
    {
        var registrations = await _registrationRepository.FindAsync(
            r => r.TournamentId == tournamentId,
            new[] { "Team", "Team.Players" }
        );
        return _mapper.Map<IEnumerable<TeamRegistrationDto>>(registrations);
    }

    public async Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId)
    {
        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("لا يوجد تسجيل لهذا الفريق.");

        var team = await _teamRepository.GetByIdAsync(teamId, new[] { "Players" });
        if (team == null) throw new NotFoundException(nameof(Team), teamId);
        if (!team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain)) throw new ForbiddenException("غير مصرح لك.");

        registration.PaymentReceiptUrl = request.PaymentReceiptUrl;
        registration.SenderNumber = request.SenderNumber;
        registration.PaymentMethod = request.PaymentMethod;
        registration.Status = RegistrationStatus.PendingPaymentReview;
        
        await _registrationRepository.UpdateAsync(registration);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Authorization: Tournament Creator or Admin
        var isOwner = tournament.CreatorUserId == userId;
        var isAdmin = userRole == UserRole.Admin.ToString();

        if (!isOwner && !isAdmin)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, new[] { "Team", "Team.Players" })).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // State Validation: Must be Pending
        if (registration.Status != RegistrationStatus.PendingPaymentReview)
        {
             throw new ConflictException($"لا يمكن اعتماد الطلب. الحالة الحالية: {registration.Status}");
        }

        registration.Status = RegistrationStatus.Approved;
        
        await _registrationRepository.UpdateAsync(registration);
        
        // Check if all teams are approved and we reached max capacity to auto-generate matches
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId);
        var activeRegistrations = allRegistrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        
        if (activeRegistrations.Count == tournament.MaxTeams && activeRegistrations.All(r => r.Status == RegistrationStatus.Approved))
        {
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
            if (!existingMatches.Any())
            {
                try {
                    await GenerateMatchesAsync(tournamentId, userId, userRole);
                    // GenerateMatchesAsync updates status to "active" and notifies
                } catch (Exception) {
                    // Fallback: If auto-generation fails, at least update registration
                    await _tournamentRepository.UpdateAsync(tournament);
                }
            }
        }

        var captainId = registration.Team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain)?.UserId;
        if (captainId != null)
        {
             await _notificationService.SendNotificationByTemplateAsync(
                 captainId.Value, 
                 NotificationTemplates.TEAM_APPROVED, 
                 new Dictionary<string, string> { 
                     { "teamName", registration.Team?.Name ?? "فريق" },
                     { "tournamentName", tournament.Name }
                 }, 
                 "registration_approved"
             );
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Authorization: Tournament Creator or Admin
        var isOwner = tournament.CreatorUserId == userId;
        var isAdmin = userRole == UserRole.Admin.ToString();

        if (!isOwner && !isAdmin)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, new[] { "Team", "Team.Players" })).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // State Validation: Must be Pending
        if (registration.Status != RegistrationStatus.PendingPaymentReview)
        {
             throw new ConflictException($"لا يمكن رفض الطلب. الحالة الحالية: {registration.Status}");
        }

        registration.Status = RegistrationStatus.Rejected;
        registration.RejectionReason = request.Reason;
        
        await _registrationRepository.UpdateAsync(registration);
        
        // Logic: Decrement only because we successfully transitioned from Pending -> Rejected
        // And Pending was counting towards capacity.
        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            
            // Re-open registration if it was closed due to capacity but no matches exist yet
            if (tournament.Status == "registration_closed" && tournament.CurrentTeams < tournament.MaxTeams)
            {
                var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
                if (!matches.Any() && DateTime.UtcNow <= tournament.RegistrationDeadline)
                {
                    tournament.Status = "registration_open";
                }
            }
            
            await _tournamentRepository.UpdateAsync(tournament);
            
            // Notify Real-Time
            var updatedTournament = await GetByIdFreshAsync(tournamentId);
            if (updatedTournament != null)
            {
                await _notifier.SendTournamentUpdatedAsync(updatedTournament);
            }
        }

        var captainId = registration.Team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain)?.UserId;
        if (captainId != null)
        {
             await _notificationService.SendNotificationByTemplateAsync(
                 captainId.Value, 
                 NotificationTemplates.TEAM_REJECTED, 
                 new Dictionary<string, string> { 
                     { "teamName", registration.Team?.Name ?? "فريق" },
                     { "tournamentName", tournament?.Name ?? "Unknown" },
                     { "reason", request.Reason }
                 }, 
                 "registration_rejected"
             );
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<IEnumerable<PendingPaymentResponse>> GetPendingPaymentsAsync(Guid? creatorId = null)
    {
        var registrations = await _registrationRepository.FindAsync(
            r => r.Status == RegistrationStatus.PendingPaymentReview && 
                (!creatorId.HasValue || r.Tournament!.CreatorUserId == creatorId.Value),
            new[] { "Team", "Tournament", "Team.Players" }
        );
        
        return registrations.Select(r => new PendingPaymentResponse
        {
            Registration = _mapper.Map<TeamRegistrationDto>(r),
            Tournament = _mapper.Map<TournamentDto>(r.Tournament)
        });
    }

    public async Task<IEnumerable<PendingPaymentResponse>> GetAllPaymentRequestsAsync(Guid? creatorId = null)
    {
        var registrations = await _registrationRepository.FindAsync(
            r => (r.Status == RegistrationStatus.PendingPaymentReview || 
                  r.Status == RegistrationStatus.Approved || 
                  r.Status == RegistrationStatus.Rejected) &&
                 (!creatorId.HasValue || r.Tournament!.CreatorUserId == creatorId.Value),
            new[] { "Team", "Tournament", "Team.Players" }
        );
        
         return registrations.Select(r => new PendingPaymentResponse
        {
            Registration = _mapper.Map<TeamRegistrationDto>(r),
            Tournament = _mapper.Map<TournamentDto>(r.Tournament)
        });
    }

    public async Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        ValidateOwnership(tournament, userId, userRole);
        
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        // Remove strictly check for < 2 if for testing, but technically correct.
        if (registrations.Count() < 2) throw new ConflictException("عدد الفرق غير كافٍ لإنشاء المباريات.");

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

        var teamIds = registrations.Select(r => r.TeamId).ToList();
        var matches = await CreateMatchesAsync(tournament, teamIds);
        
        tournament.Status = "active"; 
        await _tournamentRepository.UpdateAsync(tournament);
        
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<TournamentDto> CloseRegistrationAsync(Guid id, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole);
        
        tournament.Status = "registration_closed";
        await _tournamentRepository.UpdateAsync(tournament);
        
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<IEnumerable<GroupDto>> GetGroupsAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        var groups = new List<GroupDto>();

        // Unified Logic: RoundRobin is "Group 1". Groups formats use NumberOfGroups.
        if (tournament.Format == TournamentFormat.GroupsThenKnockout || 
            tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
             int count = tournament.NumberOfGroups > 0 ? tournament.NumberOfGroups : 1; // Fallback for legacy data
             for (int i = 1; i <= count; i++)
             {
                 groups.Add(new GroupDto { Id = i, Name = $"المجموعة {i}" });
             }
        }
        else if (tournament.Format == TournamentFormat.RoundRobin)
        {
             // Show as single group
             groups.Add(new GroupDto { Id = 1, Name = "الدوري" });
        }

        return groups;
    }

    public async Task<BracketDto> GetBracketAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Get all matches
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        
        // Filter knockout matches
        // Legacy Compatibility: 'League' matches (RoundRobin) might have GroupId null but should Not be in bracket.
        // Explicit Knockout: GroupId is null AND StageName is NOT 'League' or 'Group Stage'.
        
        var knockoutMatches = matches
            .Where(m => m.GroupId == null && m.StageName != "League" && m.StageName != "Group Stage")
            .OrderBy(m => m.RoundNumber)
            .ToList();
        
        var bracket = new BracketDto();
        var rounds = knockoutMatches.GroupBy(m => m.RoundNumber ?? 0).OrderBy(g => g.Key);
        
        foreach (var group in rounds)
        {
            var roundName = group.FirstOrDefault()?.StageName ?? $"Round {group.Key}";
            bracket.Rounds.Add(new BracketRoundDto
            {
                RoundNumber = group.Key,
                Name = roundName,
                Matches = _mapper.Map<List<MatchDto>>(group.ToList())
            });
        }
        
        return bracket;
    }

    private async Task<List<Match>> CreateMatchesAsync(Tournament tournament, List<Guid> teamIds)
    {
        var matches = new List<Match>();
        var random = new Random();
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
        var matchDate = DateTime.UtcNow.AddDays(2);
        
        if (tournament.Format == TournamentFormat.GroupsThenKnockout || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;
            
            var groups = new List<List<Guid>>();
            for (int i = 0; i < tournament.NumberOfGroups; i++) groups.Add(new List<Guid>());
            
            for (int i = 0; i < shuffledTeams.Count; i++)
            {
                groups[i % tournament.NumberOfGroups].Add(shuffledTeams[i]);
            }
            
            int dayOffset = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                         matches.Add(CreateMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage"));
                         dayOffset++;
                         
                         if (tournament.IsHomeAwayEnabled || tournament.MatchType == TournamentLegType.HomeAndAway)
                         {
                             matches.Add(CreateMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                             dayOffset++;
                         }
                    }
                }
            }
        }
        else if (tournament.Format == TournamentFormat.KnockoutOnly)
        {
            // Initial Round Only
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                 if (i + 1 < shuffledTeams.Count)
                 {
                     matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[i+1], matchDate.AddDays(i), null, 1, "Round 1"));
                     
                     if (tournament.IsHomeAwayEnabled || tournament.MatchType == TournamentLegType.HomeAndAway)
                     {
                          matches.Add(CreateMatch(tournament, shuffledTeams[i+1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1"));
                     }
                 }
            }
        }
        else // RoundRobin
        {
             // Unified: Treat Round Robin as "Group 1"
             int matchCount = 0;
             for (int i = 0; i < shuffledTeams.Count; i++)
             {
                for (int j = i + 1; j < shuffledTeams.Count; j++)
                {
                    matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League"));
                    matchCount++;
                    
                    if (tournament.IsHomeAwayEnabled || tournament.MatchType == TournamentLegType.HomeAndAway)
                    {
                        matches.Add(CreateMatch(tournament, shuffledTeams[j], shuffledTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                        matchCount++;
                    }
                }
             }
        }
        
        foreach (var m in matches)
        {
            await _matchRepository.AddAsync(m);
        }
        
        return matches;
    }

    private Match CreateMatch(Tournament t, Guid home, Guid away, DateTime date, int? group, int? round, string stage)
    {
        return new Match
        {
            TournamentId = t.Id,
            HomeTeamId = home,
            AwayTeamId = away,
            Status = MatchStatus.Scheduled,
            Date = date,
            GroupId = group,
            RoundNumber = round,
            StageName = stage,
            HomeScore = 0,
            AwayScore = 0
        };
    }

    public async Task<IEnumerable<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId, int? groupId = null)
    {
        // 1. Get all matches to determine group membership
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);

        // 2. Identify Team -> Group mapping
        var teamGroupMap = new Dictionary<Guid, int>();
        foreach (var m in matches)
        {
             int? gId = m.GroupId;
             // Legacy Fix: If RoundRobin and GroupId is null, assume Group 1
             if (gId == null && tournament?.Format == TournamentFormat.RoundRobin && m.StageName == "League")
             {
                 gId = 1;
             }

            if (gId.HasValue)
            {
                if (!teamGroupMap.ContainsKey(m.HomeTeamId)) teamGroupMap[m.HomeTeamId] = gId.Value;
                if (!teamGroupMap.ContainsKey(m.AwayTeamId)) teamGroupMap[m.AwayTeamId] = gId.Value;
            }
        }

        // 3. Filter matches by group for calculation (Finish matches only for stats)
        var relevantMatches = matches.Where(m => m.Status == MatchStatus.Finished).ToList();
        
        // Use mapped group IDs for filtering matches if necessary? 
        // Actually, cleaner to iterate all relevant matches and check their "Effective Group ID"
        
        // 4. Get all relevant registrations
        var registrations = await _registrationRepository.FindAsync(
            r => r.TournamentId == tournamentId && 
            (r.Status == RegistrationStatus.Approved || 
             r.Status == RegistrationStatus.Withdrawn ||
             r.Status == RegistrationStatus.Eliminated),
            new[] { "Team" }
        );
        
        // 5. Initialize standings
        var standings = new List<TournamentStandingDto>();
        
        foreach (var reg in registrations)
        {
            // Resolve Group
            int? teamGroup = teamGroupMap.ContainsKey(reg.TeamId) ? teamGroupMap[reg.TeamId] : null;
            
            // If RoundRobin legacy, and we are asking for Group 1 (or any), force team to Group 1 if not mapped yet
            if (tournament?.Format == TournamentFormat.RoundRobin && teamGroup == null) teamGroup = 1;

            if (groupId.HasValue && teamGroup != groupId.Value) continue;

            standings.Add(new TournamentStandingDto
            {
                TeamId = reg.TeamId,
                TeamName = reg.Team?.Name ?? "Unknown",
                TeamLogoUrl = reg.Team?.Logo,
                GroupId = teamGroup,
                Played = 0,
                Won = 0,
                Drawn = 0,
                Lost = 0,
                GoalsFor = 0,
                GoalsAgainst = 0,
                Points = 0,
                Form = new List<string>()
            });
        }

        // 6. Calculate stats
        foreach (var match in relevantMatches) 
        {
            int? effectiveGroupId = match.GroupId;
            if (effectiveGroupId == null && tournament?.Format == TournamentFormat.RoundRobin && match.StageName == "League") effectiveGroupId = 1;

            if (groupId.HasValue && effectiveGroupId != groupId.Value) continue;

            var home = standings.FirstOrDefault(s => s.TeamId == match.HomeTeamId);
            var away = standings.FirstOrDefault(s => s.TeamId == match.AwayTeamId);

            if (home == null || away == null) continue;

            home.Played++;
            away.Played++;
            
            home.GoalsFor += match.HomeScore;
            home.GoalsAgainst += match.AwayScore;
            
            away.GoalsFor += match.AwayScore;
            away.GoalsAgainst += match.HomeScore;

            if (match.HomeScore > match.AwayScore)
            {
                home.Won++;
                home.Points += 3;
                home.Form.Add("W");
                
                away.Lost++;
                away.Form.Add("L");
            }
            else if (match.AwayScore > match.HomeScore)
            {
                away.Won++;
                away.Points += 3;
                away.Form.Add("W");
                
                home.Lost++;
                home.Form.Add("L");
            }
            else
            {
                home.Drawn++;
                home.Points += 1;
                home.Form.Add("D");
                
                away.Drawn++;
                away.Points += 1;
                away.Form.Add("D");
            }
        }

        // 7. Sort
        return standings
            .OrderBy(s => s.GroupId ?? 0) // Group by GroupId first
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ToList();
    }

    public async Task EliminateTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        ValidateOwnership(tournament, userId, userRole);

        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("التسجيل غير موجود لهذا الفريق في هذه البطولة.");

        if (reg.Status == RegistrationStatus.Eliminated) return;

        reg.Status = RegistrationStatus.Eliminated;
        await _registrationRepository.UpdateAsync(reg);

        // Forfeit all UPCOMING matches for this team in this tournament
        var matches = await _matchRepository.FindAsync(m => 
            m.TournamentId == tournamentId && 
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && 
            (m.Status == MatchStatus.Scheduled || m.Status == MatchStatus.Live || m.Status == MatchStatus.Postponed));

        foreach (var match in matches)
        {
            match.Status = MatchStatus.Finished;
            match.Forfeit = true;
            
            // Assign 3-0 loss to eliminated team
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

        var team = await _teamRepository.GetByIdAsync(teamId, t => t.Players);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TEAM_ELIMINATED, 
            new Dictionary<string, string> { 
                { "teamName", team?.Name ?? "Unknown" },
                { "tournamentName", tournament.Name }
            }, 
            null, 
            "إدارة"
        );

        // Notify Captain
        if (team != null)
        {
            var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.TOURNAMENT_ELIMINATED, new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, "tournament_elimination");
            }
        }

        // Real-time update
        var updatedTournament = await GetByIdFreshAsync(tournamentId);
        if (updatedTournament != null)
        {
            await _notifier.SendTournamentUpdatedAsync(updatedTournament);
        }
    }

    public async Task<TournamentDto> EmergencyStartAsync(Guid id, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole);

        var oldStatus = tournament.Status;
        tournament.Status = "active";
        await _tournamentRepository.UpdateAsync(tournament);

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.ADMIN_OVERRIDE, 
            new Dictionary<string, string> { 
                { "action", "بدء يدوي للبطولة" },
                { "tournamentName", tournament.Name },
                { "details", $"تغيير الحالة من {oldStatus} إلى active" }
            }, 
            null, 
            "آدمن"
        );

        // Alert other admins
        await _notifier.SendSystemEventAsync("ADMIN_OVERRIDE", new { TournamentId = id, Action = "EmergencyStart" }, "role:Admin");

        return await GetByIdFreshAsync(id) ?? _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> EmergencyEndAsync(Guid id, Guid userId, string userRole)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole);

        var oldStatus = tournament.Status;
        tournament.Status = "completed";
        await _tournamentRepository.UpdateAsync(tournament);

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.ADMIN_OVERRIDE, 
            new Dictionary<string, string> { 
                { "action", "إنهاء يدوي للبطولة" },
                { "tournamentName", tournament.Name },
                { "details", $"تغيير الحالة من {oldStatus} إلى completed" }
            }, 
            null, 
            "آدمن"
        );

        // Alert other admins
        await _notifier.SendSystemEventAsync("ADMIN_OVERRIDE", new { TournamentId = id, Action = "EmergencyEnd" }, "role:Admin");

        return await GetByIdFreshAsync(id) ?? _mapper.Map<TournamentDto>(tournament);
    }

    private void ValidateOwnership(Tournament tournament, Guid userId, string userRole)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        var isOwner = userRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == userId;

        if (!isAdmin && !isOwner)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }
    }
}

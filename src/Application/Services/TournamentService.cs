using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
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
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IDistributedLock _distributedLock;

    public TournamentService(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRepository<Team> teamRepository,
        IRealTimeNotifier notifier,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        ITournamentLifecycleService lifecycleService,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _teamRepository = teamRepository;
        _notifier = notifier;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _lifecycleService = lifecycleService;
        _distributedLock = distributedLock;
    }

    public async Task<Application.Common.Models.PagedResult<TournamentDto>> GetPagedAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default)
    {
        Expression<Func<Tournament, bool>>? predicate = null;
        if (creatorId.HasValue)
        {
            predicate = t => t.CreatorUserId == creatorId.Value;
        }

        var (items, totalCount) = await _tournamentRepository.GetPagedAsync(
            page, 
            pageSize, 
            predicate, 
            q => q.OrderByDescending(t => t.StartDate),
            ct,
            t => t.Registrations, 
            t => t.WinnerTeam!);

        var tournamentList = items.ToList();
        var ids = tournamentList.Select(t => t.Id).Distinct().ToList();

        // Optimized Aggregation at SQL Level via projection
        // Note: Using ToList() (sync) because Application layer cannot reference EF Core for ToListAsync().
        // Since tournamentList is small (page size), these queries are efficient.
        var matchStats = _matchRepository.GetQueryable()
            .Where(m => ids.Contains(m.TournamentId))
            .GroupBy(m => m.TournamentId)
            .Select(g => new 
            { 
                TournamentId = g.Key, 
                TotalMatches = g.Count(),
                FinishedMatches = g.Count(m => m.Status == MatchStatus.Finished)
            })
            .ToList();

        var regStats = _registrationRepository.GetQueryable()
            .Where(r => ids.Contains(r.TournamentId) && r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn)
            .GroupBy(r => r.TournamentId)
            .Select(g => new 
            { 
                TournamentId = g.Key, 
                TotalRegistrations = g.Count(), 
                ApprovedRegistrations = g.Count(r => r.Status == RegistrationStatus.Approved) 
            })
            .ToList();

        var dtos = new List<TournamentDto>();
        foreach (var t in tournamentList)
        {
            var mStat = matchStats.FirstOrDefault(s => s.TournamentId == t.Id);
            var rStat = regStats.FirstOrDefault(s => s.TournamentId == t.Id);

            var dto = _mapper.Map<TournamentDto>(t);
            dto.RequiresAdminIntervention = CheckInterventionRequiredOptimized(t, 
                mStat?.TotalMatches ?? 0, 
                mStat?.FinishedMatches ?? 0, 
                rStat?.TotalRegistrations ?? 0, 
                rStat?.ApprovedRegistrations ?? 0, ct);
            
            // PROD-AUDIT: Manual mapping for ignored properties
            foreach (var regDto in dto.Registrations)
            {
                var sourceReg = t.Registrations.FirstOrDefault(r => r.Id == regDto.Id);
                if (sourceReg?.Team != null)
                {
                    regDto.CaptainName = sourceReg.Team.Players?
                        .FirstOrDefault(p => p.TeamRole == TeamRole.Captain)?.Name ?? string.Empty;
                }
            }
            
            dtos.Add(dto);
        }

        return new Application.Common.Models.PagedResult<TournamentDto>(dtos, totalCount, page, pageSize);
    }



    public async Task<TournamentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Use SplitQuery to prevent Cartesian Product/Join Explosion for deep includes
        var tournament = await _tournamentRepository.GetQueryable()
            .AsNoTracking()
            .Include(t => t.Registrations)
                .ThenInclude(r => r.Team)
                .ThenInclude(t => t.Players)
            .Include(t => t.WinnerTeam)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null) return null;

        var matches = await _matchRepository.GetQueryable()
            .AsNoTracking()
            .Where(m => m.TournamentId == id)
            .ToListAsync();
        
        var relevantRegs = tournament.Registrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        var totalMatches = matches.Count;
        var finishedMatches = matches.Count(m => m.Status == MatchStatus.Finished);
        
        var dto = _mapper.Map<TournamentDto>(tournament);
        dto.RequiresAdminIntervention = CheckInterventionRequiredOptimized(tournament, 
            totalMatches, 
            finishedMatches, 
            relevantRegs.Count, 
            relevantRegs.Count(r => r.Status == RegistrationStatus.Approved), ct);

        // Manual mapping for captain names
        foreach (var regDto in dto.Registrations)
        {
            var sourceReg = tournament.Registrations.FirstOrDefault(r => r.Id == regDto.Id);
            if (sourceReg?.Team?.Players != null)
            {
                regDto.CaptainName = sourceReg.Team.Players
                    .FirstOrDefault(p => p.TeamRole == TeamRole.Captain)?.Name ?? string.Empty;
            }
        }
            
        return dto;
    }

    private async Task<TournamentDto?> GetByIdFreshAsync(Guid id, CancellationToken ct = default)
    {
        return await GetByIdAsync(id, ct);
    }

    public async Task<TournamentDto?> GetActiveByTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var activeRegistration = await _registrationRepository.GetQueryable()
            .AsNoTracking()
            .Include(r => r.Tournament)
            .Where(r => r.TeamId == teamId && 
                        r.Status == RegistrationStatus.Approved && 
                        r.Tournament!.Status == TournamentStatus.Active)
            .Select(r => r.Tournament)
            .FirstOrDefaultAsync(ct);

        return activeRegistration != null ? _mapper.Map<TournamentDto>(activeRegistration) : null;
    }

    public async Task<TeamRegistrationDto?> GetRegistrationByTeamAsync(Guid tournamentId, Guid teamId, CancellationToken ct = default)
    {
        var registration = await _registrationRepository.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct);

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    private bool CheckInterventionRequiredOptimized(Tournament tournament, int totalMatches, int finishedMatches, int totalRegs, int approvedRegs, CancellationToken ct)
    {
        // Case 1: Registration closed but should be active (All approved, capacity reached, but no matches)
        if (tournament.Status == TournamentStatus.RegistrationClosed)
        {
            if (totalRegs == tournament.MaxTeams && approvedRegs == totalRegs && totalMatches == 0)
            {
                 return true;
            }
            
            // Deadline passed but no matches generated
            if (DateTime.UtcNow > tournament.StartDate && totalMatches == 0)
            {
                return true;
            }
        }

        // Case 2: Active but stuck?
        if (tournament.Status == TournamentStatus.Active)
        {
            // No matches at all but active
            if (totalMatches == 0) return true;

            // All matches finished but status is not completed
            if (totalMatches > 0 && totalMatches == finishedMatches) return true;

            // Past end date but not completed
            if (DateTime.UtcNow > tournament.EndDate.AddDays(2)) return true;
        }

        // Case 3: Completed but missing winner?
        if (tournament.Status == TournamentStatus.Completed && tournament.WinnerTeamId == null && totalMatches > 0)
        {
            return true;
        }

        return false;
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid? creatorId = null, CancellationToken ct = default)
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
            Status = TournamentStatus.RegistrationOpen,
            Format = request.Format,
            MatchType = request.MatchType,
            NumberOfGroups = request.NumberOfGroups,
            QualifiedTeamsPerGroup = request.QualifiedTeamsPerGroup,
            WalletNumber = request.WalletNumber,
            InstaPayNumber = request.InstaPayNumber,
            IsHomeAwayEnabled = request.IsHomeAwayEnabled,
            SeedingMode = request.SeedingMode,
            PaymentMethodsJson = request.PaymentMethodsJson,
            Mode = request.Mode,
            AllowLateRegistration = request.AllowLateRegistration,
            LateRegistrationMode = request.LateRegistrationMode
        };

        if (request.Mode.HasValue)
        {
            (tournament.Format, tournament.MatchType) = MapModeToLegacy(request.Mode.Value, ct);
        }

        await _tournamentRepository.AddAsync(tournament, ct);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TOURNAMENT_CREATED, 
            new Dictionary<string, string> { { "tournamentName", tournament.Name } }, 
            null, 
            "آدمن"
        , ct);
        
        // Real-time Event
        await _notifier.SendTournamentCreatedAsync(_mapper.Map<TournamentDto>(tournament));
        
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Players", "WinnerTeam" }, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);
        ValidateOwnership(tournament, userId, userRole, ct);

        // Safety Check: Format Change
        if (request.Format.HasValue && request.Format.Value != tournament.Format)
        {
            var matches = await _matchRepository.FindAsync(m => m.TournamentId == id, ct);
            // If any knockout match exists (GroupId == null AND Stage != League) and is started/finished, block change
            var hasActiveKnockout = matches.Any(m => m.GroupId == null && m.StageName != "League" && m.Status != MatchStatus.Scheduled);
            
            if (hasActiveKnockout)
            {
                throw new ConflictException("لا يمكن تغيير نظام البطولة بعد بدء الأدوار الإقصائية.");
            }
        }

        if (request.Name != null) tournament.Name = request.Name;
        if (request.Description != null) tournament.Description = request.Description;
        if (request.Status != null && Enum.TryParse<TournamentStatus>(request.Status, true, out var newStatus)) 
            tournament.Status = newStatus;
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
        if (request.Mode.HasValue) 
        {
            tournament.Mode = request.Mode.Value;
            (tournament.Format, tournament.MatchType) = MapModeToLegacy(request.Mode.Value, ct);
        }
        if (request.OpeningMatchId.HasValue) tournament.OpeningMatchId = request.OpeningMatchId.Value;
        if (request.AllowLateRegistration.HasValue) tournament.AllowLateRegistration = request.AllowLateRegistration.Value;
        if (request.LateRegistrationMode.HasValue) tournament.LateRegistrationMode = request.LateRegistrationMode.Value;
        if (request.OpeningMatchHomeTeamId.HasValue) tournament.OpeningMatchHomeTeamId = request.OpeningMatchHomeTeamId.Value;
        if (request.OpeningMatchAwayTeamId.HasValue) tournament.OpeningMatchAwayTeamId = request.OpeningMatchAwayTeamId.Value;

        // Force auto-closure if capacity reached after manual update
        if (tournament.CurrentTeams >= tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationOpen)
        {
            tournament.Status = TournamentStatus.RegistrationClosed;
        }
        else if (tournament.CurrentTeams < tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationClosed && !(await _matchRepository.FindAsync(m => m.TournamentId == id, ct)).Any())
        {
            // Re-open if capacity was increased and no matches exist
            if (DateTime.UtcNow <= tournament.RegistrationDeadline)
            {
                tournament.Status = TournamentStatus.RegistrationOpen;
            }
        }

        await _tournamentRepository.UpdateAsync(tournament, ct);

        var dto = await GetByIdFreshAsync(id, ct);

        // Real-time Event
        if (dto != null)
        {
            await _notifier.SendTournamentUpdatedAsync(dto, ct);
        }

        return dto ?? _mapper.Map<TournamentDto>(tournament);
    }

    public async Task DeleteAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole, ct);

        // Delete associated matches and registrations logic normally handled by DB constraints or repository
        // For now, assuming direct delete is safe or handled
        await _tournamentRepository.DeleteAsync(tournament, ct);
    }


    public async Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId, CancellationToken ct = default)
    {
        var lockKey = $"tournament_registration_{tournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30), ct))
        {
            throw new ConflictException("النظام مشغول حالياً، يرجى المحاولة مرة أخرى.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations" }, ct);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

            if (DateTime.UtcNow > tournament.RegistrationDeadline && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("انتهى موعد التسجيل في البطولة.");
            }

            // ATOMIC CAPACITY CHECK
            bool isActive = tournament.Status == TournamentStatus.Active;
            bool isFull = tournament.Registrations.Count >= tournament.MaxTeams;

            if (isFull && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("اكتمل عدد الفرق في البطولة.");
            }

            if (isActive && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("بدأت البطولة بالفعل ولا يمكن التسجيل حالياً.");
            }

            var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, ct);
            if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

            // Part 4 Logic: Late Registration
            RegistrationStatus targetStatus = RegistrationStatus.PendingPaymentReview;
            if (isActive || isFull)
            {
                if (tournament.LateRegistrationMode == LateRegistrationMode.WaitingList)
                {
                    targetStatus = RegistrationStatus.WaitingList;
                }
                else if (tournament.LateRegistrationMode == LateRegistrationMode.ReplaceIfNoMatchPlayed)
                {
                    // Logic: Replacement will be handled by Admin manually or we mark as pending for replacement review
                    targetStatus = RegistrationStatus.PendingPaymentReview;
                }
                else
                {
                     throw new ConflictException("التسجيل المتأخر غير متاح حالياً.");
                }
            }

            if (!team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain))
            {
                throw new ForbiddenException("فقط كابتن الفريق يمكنه تسجيل الفريق في البطولات.");
            }

            if (tournament.Registrations.Any(r => r.TeamId == request.TeamId && r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn))
            {
                throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة أو قيد المراجعة.");
            }

            var registration = new TeamRegistration
            {
                TournamentId = tournamentId,
                TeamId = request.TeamId,
                Status = targetStatus
            };

            if (targetStatus != RegistrationStatus.WaitingList)
                tournament.CurrentTeams++;

            if (tournament.EntryFee <= 0 && targetStatus != RegistrationStatus.WaitingList)
            {
                registration.Status = RegistrationStatus.Approved;
            }

            if (tournament.CurrentTeams >= tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationOpen)
            {
                tournament.Status = TournamentStatus.RegistrationClosed;
            }

            await _registrationRepository.AddAsync(registration, ct);
            await _tournamentRepository.UpdateAsync(tournament, ct);

            var registrationDto = _mapper.Map<TeamRegistrationDto>(registration);

            // Notify Real-Time
            var tournamentDto = await GetByIdFreshAsync(tournamentId, ct);
            if (tournamentDto != null)
            {
                await _notifier.SendTournamentUpdatedAsync(tournamentDto, ct);
            }

            return registrationDto;
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
             throw new ConflictException("حدث خطأ أثناء التسجيل بسبب ضغط الطلبات. يرجى المحاولة مرة أخرى.");
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateException" && ex.InnerException?.Message.Contains("UQ_TeamRegistration_Tournament_Team") == true)
        {
             throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة.");
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, ct);
        }
    }

    public async Task<Application.Common.Models.PagedResult<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        
        var (items, totalCount) = await _registrationRepository.GetPagedAsync(
            page, 
            pageSize, 
            r => r.TournamentId == tournamentId,
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.Team!, 
            r => r.Team!.Players
        );

        var dtos = _mapper.Map<List<TeamRegistrationDto>>(items);
        return new Application.Common.Models.PagedResult<TeamRegistrationDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId, CancellationToken ct = default)
    {
        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("لا يوجد تسجيل لهذا الفريق.");

        var team = await _teamRepository.GetByIdAsync(teamId, new[] { "Players" }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);
        if (!team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain)) throw new ForbiddenException("غير مصرح لك.");

        registration.PaymentReceiptUrl = request.PaymentReceiptUrl;
        registration.SenderNumber = request.SenderNumber;
        registration.PaymentMethod = request.PaymentMethod;
        registration.Status = RegistrationStatus.PendingPaymentReview;
        
        await _registrationRepository.UpdateAsync(registration, ct);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Authorization: Tournament Creator or Admin
        var isOwner = tournament.CreatorUserId == userId;
        var isAdmin = userRole == UserRole.Admin.ToString();

        if (!isOwner && !isAdmin)
        {
            throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, new[] { "Team", "Team.Players" }, ct)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // State Validation: Must be Pending or WaitingList (if promoted)
        if (registration.Status != RegistrationStatus.PendingPaymentReview && registration.Status != RegistrationStatus.WaitingList)
        {
            throw new ConflictException($"لا يمكن اعتماد الطلب. الحالة الحالية: {registration.Status}");
        }

        // PART 1: Duplicate Player Participation Prevention
        var playerIds = registration.Team?.Players.Select(p => p.Id).ToList() ?? new List<Guid>();
        if (playerIds.Any())
        {
            var existingParticipations = await _tournamentPlayerRepository.FindAsync(tp => tp.TournamentId == tournamentId && playerIds.Contains(tp.PlayerId));
            if (existingParticipations.Any())
            {
                throw new ConflictException("واحد أو أكثر من اللاعبين مسجل بالفعل في فريق آخر في هذه البطولة.");
            }
        }

        registration.Status = RegistrationStatus.Approved;
        
        // Use Domain Event for reliable processing via Outbox
        var captain = registration.Team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain?.UserId != null)
        {
            registration.AddDomainEvent(new Domain.Events.TournamentRegistrationApprovedEvent(
                tournamentId,
                teamId,
                captain.UserId.Value,
                tournament.Name,
                registration.Team?.Name ?? "فريق"
            ));
        }

        await _registrationRepository.UpdateAsync(registration, ct);

        // Populate TournamentPlayers tracking
        if (registration.Team?.Players != null)
        {
            var participations = registration.Team.Players
                .Select(p => new TournamentPlayer
                {
                    TournamentId = tournamentId,
                    PlayerId = p.Id,
                    TeamId = registration.TeamId,
                    RegistrationId = registration.Id
                }).ToList();
            
            await _tournamentPlayerRepository.AddRangeAsync(participations);
        }
        
        // Check if all teams are approved and we reached max capacity to auto-generate matches
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId, ct);
        var activeRegistrations = allRegistrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        
        if (activeRegistrations.Count == tournament.MaxTeams && activeRegistrations.All(r => r.Status == RegistrationStatus.Approved))
        {
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
            if (!existingMatches.Any() && tournament.Status != TournamentStatus.Active)
            {
                await GenerateMatchesAsync(tournamentId, userId, userRole);
            }
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Authorization: Tournament Creator or Admin
        var isOwner = tournament.CreatorUserId == userId;
        var isAdmin = userRole == UserRole.Admin.ToString();

        if (!isOwner && !isAdmin)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, new[] { "Team", "Team.Players" }, ct)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // State Validation: Must be Pending
        if (registration.Status != RegistrationStatus.PendingPaymentReview)
        {
             throw new ConflictException($"لا يمكن رفض الطلب. الحالة الحالية: {registration.Status}");
        }

        registration.Status = RegistrationStatus.Rejected;
        registration.RejectionReason = request.Reason;
        
        // Use Domain Event for reliable processing via Outbox
        var captain = registration.Team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain?.UserId != null)
        {
            registration.AddDomainEvent(new Domain.Events.TournamentRegistrationRejectedEvent(
                tournamentId,
                teamId,
                captain.UserId.Value,
                tournament.Name,
                registration.Team?.Name ?? "فريق",
                request.Reason
            ));
        }

        await _registrationRepository.UpdateAsync(registration, ct);

        // Cleanup TournamentPlayers
        var participations = await _tournamentPlayerRepository.FindAsync(tp => tp.RegistrationId == registration.Id, ct);
        await _tournamentPlayerRepository.DeleteRangeAsync(participations, ct);
        
        // Logic: Decrement only because we successfully transitioned from Pending -> Rejected
        // And Pending was counting towards capacity.
        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            
            // Re-open registration if it was closed due to capacity but no matches exist yet
            if (tournament.Status == TournamentStatus.RegistrationClosed && tournament.CurrentTeams < tournament.MaxTeams)
            {
                var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
                if (!matches.Any() && DateTime.UtcNow <= tournament.RegistrationDeadline)
                {
                    tournament.Status = TournamentStatus.RegistrationOpen;
                }
            }
            
            // Logic for promotion from Waiting List
            var waiting = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.WaitingList, ct)).OrderBy(r => r.CreatedAt).FirstOrDefault();
            if (waiting != null)
            {
                // Automate waiting list promotion if a slot opens
                // We'll leave it as manual for Admin to Approve, but we could auto-promote here.
            }
            
            await _tournamentRepository.UpdateAsync(tournament, ct);
            
            // Notify Real-Time
            var updatedTournament = await GetByIdFreshAsync(tournamentId, ct);
            if (updatedTournament != null)
            {
                await _notifier.SendTournamentUpdatedAsync(updatedTournament, ct);
            }
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<Application.Common.Models.PagedResult<PendingPaymentResponse>> GetPendingPaymentsAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _registrationRepository.GetPagedAsync(
            page,
            pageSize,
            r => r.Status == RegistrationStatus.PendingPaymentReview && 
                (!creatorId.HasValue || r.Tournament!.CreatorUserId == creatorId.Value),
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.Team!, 
            r => r.Tournament!, 
            r => r.Team!.Players
        );
        
        var dtos = items.Select(r => new PendingPaymentResponse
        {
            Registration = _mapper.Map<TeamRegistrationDto>(r),
            Tournament = _mapper.Map<TournamentDto>(r.Tournament)
        }).ToList();

        return new Application.Common.Models.PagedResult<PendingPaymentResponse>(dtos, totalCount, page, pageSize);
    }

    public async Task<Application.Common.Models.PagedResult<PendingPaymentResponse>> GetAllPaymentRequestsAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _registrationRepository.GetPagedAsync(
            page,
            pageSize,
            r => (r.Status == RegistrationStatus.PendingPaymentReview || 
                  r.Status == RegistrationStatus.Approved || 
                  r.Status == RegistrationStatus.Rejected) &&
                 (!creatorId.HasValue || r.Tournament!.CreatorUserId == creatorId.Value),
            q => q.OrderByDescending(r => r.CreatedAt),
            ct,
            r => r.Team!, 
            r => r.Tournament!, 
            r => r.Team!.Players
        );
        
        var dtos = items.Select(r => new PendingPaymentResponse
        {
            Registration = _mapper.Map<TeamRegistrationDto>(r),
            Tournament = _mapper.Map<TournamentDto>(r.Tournament)
        }).ToList();

        return new Application.Common.Models.PagedResult<PendingPaymentResponse>(dtos, totalCount, page, pageSize);
    }

    public async Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        ValidateOwnership(tournament, userId, userRole, ct);
        
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved, ct);
        // Remove strictly check for < 2 if for testing, but technically correct.
        if (registrations.Count() < 2) throw new ConflictException("عدد الفرق غير كافٍ لإنشاء المباريات.");

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

        var teamIds = registrations.Select(r => r.TeamId).ToList();
        var matches = await CreateMatchesAsync(tournament, teamIds, ct);
        
        tournament.Status = TournamentStatus.Active; 
        await _tournamentRepository.UpdateAsync(tournament, ct);
        
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<TournamentDto> CloseRegistrationAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole, ct);
        
        tournament.Status = TournamentStatus.RegistrationClosed;
        await _tournamentRepository.UpdateAsync(tournament, ct);
        
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<Application.Common.Models.PagedResult<GroupDto>> GetGroupsAsync(Guid tournamentId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
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

        var totalCount = groups.Count;
        var items = groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new Application.Common.Models.PagedResult<GroupDto>(items, totalCount, page, pageSize);
    }

    public async Task<BracketDto> GetBracketAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Get all matches
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        
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

    private async Task<List<Match>> CreateMatchesAsync(Tournament tournament, List<Guid> teamIds, CancellationToken ct = default)
    {
        var matches = new List<Match>();
        var random = new Random();
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();
        
        if (effectiveMode == TournamentMode.GroupsKnockoutSingle || effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;
            
            var groups = new List<List<Guid>>();
            for (int i = 0; i < tournament.NumberOfGroups; i++) groups.Add(new List<Guid>());
            
            for (int i = 0; i < shuffledTeams.Count; i++)
            {
                groups[i % tournament.NumberOfGroups].Add(shuffledTeams[i]);
            }
            
            int dayOffset = 0;
            bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                         matches.Add(CreateMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage", ct));
                         dayOffset++;
                         
                         if (isHomeAway)
                         {
                             matches.Add(CreateMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage", ct));
                             dayOffset++;
                         }
                    }
                }
            }
        }
        else if (effectiveMode == TournamentMode.KnockoutSingle || effectiveMode == TournamentMode.KnockoutHomeAway)
        {
            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                 if (i + 1 < shuffledTeams.Count)
                 {
                     matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[i+1], matchDate.AddDays(i), null, 1, "Round 1", ct));
                     
                     if (isHomeAway)
                     {
                          matches.Add(CreateMatch(tournament, shuffledTeams[i+1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1", ct));
                     }
                 }
            }
        }
        else // League modes
        {
             bool isHomeAway = effectiveMode == TournamentMode.LeagueHomeAway;
             int matchCount = 0;
             for (int i = 0; i < shuffledTeams.Count; i++)
             {
                for (int j = i + 1; j < shuffledTeams.Count; j++)
                {
                    matches.Add(CreateMatch(tournament, shuffledTeams[i], shuffledTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League", ct));
                    matchCount++;
                    
                    if (isHomeAway)
                    {
                        matches.Add(CreateMatch(tournament, shuffledTeams[j], shuffledTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League", ct));
                        matchCount++;
                    }
                }
             }
        }
        
        await _matchRepository.AddRangeAsync(matches);
        
        return matches;
    }

    private Match CreateMatch(Tournament t, Guid home, Guid away, DateTime date, int? group, int? round, string stage, CancellationToken ct)
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

    public async Task<Application.Common.Models.PagedResult<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId, int page, int pageSize, int? groupId = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        // 1. Get all matches with events
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, new[] { "Events" }, ct);
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);

        // 2. Get all relevant registrations
        var registrations = await _registrationRepository.FindAsync(
            r => r.TournamentId == tournamentId && 
            (r.Status == RegistrationStatus.Approved || 
             r.Status == RegistrationStatus.Withdrawn ||
             r.Status == RegistrationStatus.Eliminated),
            new[] { "Team" }
        );
        
        // 3. Delegate to Lifecycle Service
        var allStandings = _lifecycleService.CalculateStandings(matches, registrations);

        // 4. Filter by group if requested
        var query = allStandings.AsQueryable();
        if (groupId.HasValue)
        {
            query = query.Where(s => s.GroupId == groupId.Value);
        }

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new Application.Common.Models.PagedResult<TournamentStandingDto>(items, totalCount, page, pageSize);
    }

    public async Task EliminateTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        ValidateOwnership(tournament, userId, userRole, ct);

        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("التسجيل غير موجود لهذا الفريق في هذه البطولة.");

        if (reg.Status == RegistrationStatus.Eliminated) return;

        reg.Status = RegistrationStatus.Eliminated;
        await _registrationRepository.UpdateAsync(reg, ct);

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
            
            await _matchRepository.UpdateAsync(match, ct);
        }

        var team = await _teamRepository.GetByIdAsync(teamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TEAM_ELIMINATED, 
            new Dictionary<string, string> { 
                { "teamName", team?.Name ?? "Unknown" },
                { "tournamentName", tournament.Name }
            }, 
            null, 
            "إدارة"
        , ct);

        // Notify Captain
        if (team != null)
        {
            var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.TOURNAMENT_ELIMINATED, new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, "tournament_elimination", ct);
            }
        }

        // Real-time update
        var updatedTournament = await GetByIdFreshAsync(tournamentId, ct);
        if (updatedTournament != null)
        {
            await _notifier.SendTournamentUpdatedAsync(updatedTournament, ct);
        }
    }

    public async Task<TournamentDto> EmergencyStartAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole, ct);

        var oldStatus = tournament.Status;
        tournament.Status = TournamentStatus.Active;
        await _tournamentRepository.UpdateAsync(tournament, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.ADMIN_OVERRIDE, 
            new Dictionary<string, string> { 
                { "action", "بدء يدوي للبطولة" },
                { "tournamentName", tournament.Name },
                { "details", $"تغيير الحالة من {oldStatus} إلى {TournamentStatus.Active}" }
            }, 
            null, 
            "آدمن"
        , ct);

        // Alert other admins
        await _notifier.SendSystemEventAsync("ADMIN_OVERRIDE", new { TournamentId = id, Action = "EmergencyStart" }, "role:Admin", ct);

        return await GetByIdFreshAsync(id, ct) ?? _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> EmergencyEndAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole, ct);

        var oldStatus = tournament.Status;
        tournament.Status = TournamentStatus.Completed;
        await _tournamentRepository.UpdateAsync(tournament, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.ADMIN_OVERRIDE, 
            new Dictionary<string, string> { 
                { "action", "إنهاء يدوي للبطولة" },
                { "tournamentName", tournament.Name },
                { "details", $"تغيير الحالة من {oldStatus} إلى {TournamentStatus.Completed}" }
            }, 
            null, 
            "آدمن"
        , ct);

        // Alert other admins
        await _notifier.SendSystemEventAsync("ADMIN_OVERRIDE", new { TournamentId = id, Action = "EmergencyEnd" }, "role:Admin", ct);

        return await GetByIdFreshAsync(id, ct) ?? _mapper.Map<TournamentDto>(tournament);
    }

    private void ValidateOwnership(Tournament tournament, Guid userId, string userRole, CancellationToken ct)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        var isOwner = userRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == userId;

        if (!isAdmin && !isOwner)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }
    }

    public async Task<IEnumerable<MatchDto>> SetOpeningMatchAsync(Guid tournamentId, Guid homeTeamId, Guid awayTeamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        ValidateOwnership(tournament, userId, userRole, ct);

        if (tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
        {
            throw new ConflictException("لا يمكن تحديد مباراة الافتتاح في هذه المرحلة.");
        }

        if (homeTeamId == awayTeamId) throw new ConflictException("لا يمكن اختيار نفس الفريق للمباراة.");

        tournament.OpeningMatchHomeTeamId = homeTeamId;
        tournament.OpeningMatchAwayTeamId = awayTeamId;
        
        await _tournamentRepository.UpdateAsync(tournament, ct);

        // Trigger Knockout R1 Generation
        await _lifecycleService.GenerateKnockoutR1Async(tournamentId, ct);
        
        // Refresh tournament and return generated matches
        var generatedMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId && m.GroupId == null, ct);
        return _mapper.Map<IEnumerable<MatchDto>>(generatedMatches);
    }

    public async Task WithdrawTeamAsync(Guid tournamentId, Guid teamId, Guid userId, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        if (tournament.Status != TournamentStatus.RegistrationOpen && tournament.Status != TournamentStatus.RegistrationClosed)
        {
            throw new ConflictException("لا يمكن الانسحاب من البطولة بعد بدئها.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // Only captain can withdraw
        var team = await _teamRepository.GetByIdAsync(teamId, new[] { "Players" }, ct);
        if (team == null || !team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain))
        {
            throw new ForbiddenException("فقط كابتن الفريق يمكنه سحب الفريق من البطولة.");
        }

        registration.Status = RegistrationStatus.Withdrawn;
        await _registrationRepository.UpdateAsync(registration, ct);

        // Cleanup participation
        var participations = await _tournamentPlayerRepository.FindAsync(tp => tp.RegistrationId == registration.Id, ct);
        foreach (var p in participations) await _tournamentPlayerRepository.DeleteAsync(p, ct);

        tournament.CurrentTeams--;
        await _tournamentRepository.UpdateAsync(tournament, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            "TEAM_WITHDRAWN", 
            new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, 
            userId, 
            "كابتن الفريق"
        , ct);
    }

    public async Task<TeamRegistrationDto> PromoteWaitingTeamAsync(Guid tournamentId, Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        ValidateOwnership(tournament, userId, userRole, ct);

        if (tournament.CurrentTeams >= tournament.MaxTeams)
        {
            throw new ConflictException("البطولة مكتملة العدد بالفعل.");
        }

        var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct)).FirstOrDefault();
        if (registration == null || registration.Status != RegistrationStatus.WaitingList)
        {
            throw new NotFoundException("الفريق غير موجود في قائمة الانتظار.");
        }

        // Promote to PendingPayment (or Approved if free)
        registration.Status = tournament.EntryFee > 0 ? RegistrationStatus.PendingPayment : RegistrationStatus.Approved;
        await _registrationRepository.UpdateAsync(registration, ct);

        tournament.CurrentTeams++;
        await _tournamentRepository.UpdateAsync(tournament, ct);

        await _analyticsService.LogActivityByTemplateAsync(
            "TEAM_PROMOTED", 
            new Dictionary<string, string> { { "teamName", registration.Team?.Name ?? teamId.ToString() }, { "tournamentName", tournament.Name } }, 
            userId, 
            "المنظم"
        );

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<IEnumerable<MatchDto>> GenerateManualMatchesAsync(Guid tournamentId, ManualDrawRequest request, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        ValidateOwnership(tournament, userId, userRole, ct);

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل. قم بحذفها أولاً لإعادة التوليد.");

        var matches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();

        if (request.GroupAssignments != null && request.GroupAssignments.Any())
        {
            foreach (var group in request.GroupAssignments)
            {
                var teams = group.TeamIds;
                bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway || effectiveMode == TournamentMode.LeagueHomeAway;

                for (int i = 0; i < teams.Count; i++)
                {
                    for (int j = i + 1; j < teams.Count; j++)
                    {
                        matches.Add(CreateMatch(tournament, teams[i], teams[j], matchDate, group.GroupId, 1, "Group Stage", ct));
                        matchDate = matchDate.AddHours(2);
                        if (isHomeAway)
                        {
                            matches.Add(CreateMatch(tournament, teams[j], teams[i], matchDate, group.GroupId, 1, "Group Stage", ct));
                            matchDate = matchDate.AddHours(2);
                        }
                    }
                }
            }
        }
        else if (request.KnockoutPairings != null && request.KnockoutPairings.Any())
        {
            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            foreach (var pairing in request.KnockoutPairings)
            {
                matches.Add(CreateMatch(tournament, pairing.HomeTeamId, pairing.AwayTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName, ct));
                matchDate = matchDate.AddHours(2);
                if (isHomeAway)
                {
                    matches.Add(CreateMatch(tournament, pairing.AwayTeamId, pairing.HomeTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName, ct));
                    matchDate = matchDate.AddHours(2);
                }
            }
        }

        await _matchRepository.AddRangeAsync(matches);
        tournament.Status = TournamentStatus.Active;
        await _tournamentRepository.UpdateAsync(tournament, ct);

        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    private (TournamentFormat Format, TournamentLegType MatchType) MapModeToLegacy(TournamentMode mode, CancellationToken ct)
    {
        return mode switch
        {
            TournamentMode.LeagueSingle => (TournamentFormat.RoundRobin, TournamentLegType.SingleLeg),
            TournamentMode.LeagueHomeAway => (TournamentFormat.RoundRobin, TournamentLegType.HomeAndAway),
            TournamentMode.GroupsKnockoutSingle => (TournamentFormat.GroupsThenKnockout, TournamentLegType.SingleLeg),
            TournamentMode.GroupsKnockoutHomeAway => (TournamentFormat.GroupsThenKnockout, TournamentLegType.HomeAndAway),
            TournamentMode.KnockoutSingle => (TournamentFormat.KnockoutOnly, TournamentLegType.SingleLeg),
            TournamentMode.KnockoutHomeAway => (TournamentFormat.KnockoutOnly, TournamentLegType.HomeAndAway),
            _ => (TournamentFormat.RoundRobin, TournamentLegType.SingleLeg)
        };
    }
    public async Task ProcessAutomatedStateTransitionsAsync(CancellationToken ct = default)
    {
        // 1. Close Registration for Expired Deadlines
        var openTournaments = await _tournamentRepository.FindAsync(t => t.Status == TournamentStatus.RegistrationOpen && t.RegistrationDeadline < DateTime.UtcNow, ct);
        foreach (var t in openTournaments)
        {
             t.Status = TournamentStatus.RegistrationClosed;
             await _tournamentRepository.UpdateAsync(t, ct); // TransactionBehavior will commit this? No, pipeline wraps Command. Yes.
        }
        
        // 2. Start Tournament for Scheduled Start Dates (Simulated, real logic might need match generation)
        // Only start if Registration is Closed (or force close?)
        // Let's assume strict flow: Open -> Closed -> Active
        var readyTournaments = await _tournamentRepository.FindAsync(t => t.Status == TournamentStatus.RegistrationClosed && t.StartDate <= DateTime.UtcNow, ct);
        foreach (var t in readyTournaments)
        {
             t.Status = TournamentStatus.Active;
             await _tournamentRepository.UpdateAsync(t, ct);
        }
    }
}

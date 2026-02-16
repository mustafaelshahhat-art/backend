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

    public async Task<Application.Common.Models.PagedResult<TournamentDto>> GetPagedAsync(int page, int pageSize, Guid? creatorId = null, bool includeDrafts = false, CancellationToken ct = default)
    {
        var query = _tournamentRepository.GetQueryable().AsNoTracking();

        if (creatorId.HasValue)
        {
            query = query.Where(t => t.CreatorUserId == creatorId.Value);
        }
        else if (!includeDrafts)
        {
            query = query.Where(t => t.Status != TournamentStatus.Draft);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new 
            {
                Tournament = t,
                WinnerTeamName = t.WinnerTeam != null ? t.WinnerTeam.Name : null,
                TotalMatches = t.Matches.Count(),
                FinishedMatches = t.Matches.Count(m => m.Status == MatchStatus.Finished),
                TotalRegs = t.Registrations.Count(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn),
                ApprovedRegs = t.Registrations.Count(r => r.Status == RegistrationStatus.Approved),
                Registrations = t.Registrations
                    .Where(r => r.Status != RegistrationStatus.Rejected)
                    .Select(r => new 
                    {
                        Registration = r,
                        TeamName = r.Team != null ? r.Team.Name : string.Empty,
                        TeamLogoUrl = r.Team != null ? r.Team.Logo : null,
                        CaptainName = r.Team != null && r.Team.Players != null 
                            ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty 
                            : string.Empty
                    }).ToList()
            })
            .ToListAsync(ct);

        var dtos = new List<TournamentDto>();
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var dto = _mapper.Map<TournamentDto>(item.Tournament);
            dto.WinnerTeamName = item.WinnerTeamName;
            
            // Map registrations
            dto.Registrations = item.Registrations.Select(r => 
            {
                var regDto = _mapper.Map<TeamRegistrationDto>(r.Registration);
                regDto.TeamName = r.TeamName;
                regDto.TeamLogoUrl = r.TeamLogoUrl;
                regDto.CaptainName = r.CaptainName;
                return regDto;
            }).ToList();

            // Calculate intervention
            dto.RequiresAdminIntervention = CheckInterventionRequiredInternal(item.Tournament, 
                item.TotalMatches, 
                item.FinishedMatches, 
                item.TotalRegs, 
                item.ApprovedRegs, 
                now);
            
            dtos.Add(dto);
        }

        return new Application.Common.Models.PagedResult<TournamentDto>(dtos, totalCount, page, pageSize);
    }

    private bool CheckInterventionRequiredInternal(Tournament tournament, int totalMatches, int finishedMatches, int totalRegs, int approvedRegs, DateTime now)
    {
        // Case 1: Registration closed but should be active (All approved, capacity reached, but no matches)
        if (tournament.Status == TournamentStatus.RegistrationClosed)
        {
            if (totalRegs == tournament.MaxTeams && approvedRegs == totalRegs && totalMatches == 0)
            {
                 return true;
            }
            
            // Deadline passed but no matches generated
            if (now > tournament.StartDate && totalMatches == 0)
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
            if (now > tournament.EndDate.AddDays(2)) return true;
        }

        // Case 3: Completed but missing winner?
        if (tournament.Status == TournamentStatus.Completed && tournament.WinnerTeamId == null && totalMatches > 0)
        {
            return true;
        }

        return false;
    }



    public async Task<TournamentDto?> GetByIdAsync(Guid id, Guid? userId = null, string? userRole = null, CancellationToken ct = default)
    {
        var item = await _tournamentRepository.GetQueryable()
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new 
            {
                Tournament = t,
                WinnerTeamName = t.WinnerTeam != null ? t.WinnerTeam.Name : null,
                TotalMatches = t.Matches.Count(),
                FinishedMatches = t.Matches.Count(m => m.Status == MatchStatus.Finished),
                TotalRegs = t.Registrations.Count(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn),
                ApprovedRegs = t.Registrations.Count(r => r.Status == RegistrationStatus.Approved),
                Registrations = t.Registrations
                    .Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn)
                    .Select(r => new 
                    {
                        Registration = r,
                        TeamName = r.Team != null ? r.Team.Name : string.Empty,
                        TeamLogoUrl = r.Team != null ? r.Team.Logo : null,
                        CaptainName = r.Team != null && r.Team.Players != null 
                            ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty 
                            : string.Empty
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (item == null) return null;

        // PRIVACY: Privacy filter for Drafts
        if (item.Tournament.Status == TournamentStatus.Draft && item.Tournament.CreatorUserId != userId && userRole != "Admin")
        {
            return null;
        }

        var dto = _mapper.Map<TournamentDto>(item.Tournament);
        dto.WinnerTeamName = item.WinnerTeamName;
        
        // Map registrations
        dto.Registrations = item.Registrations.Select(r => 
        {
            var regDto = _mapper.Map<TeamRegistrationDto>(r.Registration);
            regDto.TeamName = r.TeamName;
            regDto.TeamLogoUrl = r.TeamLogoUrl;
            regDto.CaptainName = r.CaptainName;
            return regDto;
        }).ToList();

        // Calculate intervention
        dto.RequiresAdminIntervention = CheckInterventionRequiredInternal(item.Tournament, 
            item.TotalMatches, 
            item.FinishedMatches, 
            item.TotalRegs, 
            item.ApprovedRegs, 
            DateTime.UtcNow);
            
        return dto;
    }

    private async Task<TournamentDto?> GetByIdFreshAsync(Guid id, CancellationToken ct = default)
    {
        return await GetByIdAsync(id, null, null, ct);
    }

    public async Task<TournamentDto?> GetActiveByTeamAsync(Guid teamId, CancellationToken ct = default)
    {
        var activeTournament = await _registrationRepository.GetQueryable()
            .AsNoTracking()
            .Include(r => r.Tournament)
            .Where(r => r.TeamId == teamId && 
                        r.Status == RegistrationStatus.Approved && 
                        r.Tournament!.Status == TournamentStatus.Active)
            .Select(r => r.Tournament)
            .FirstOrDefaultAsync(ct);

        return activeTournament != null ? _mapper.Map<TournamentDto>(activeTournament) : null;
    }

    public async Task<TeamRegistrationDto?> GetRegistrationByTeamAsync(Guid tournamentId, Guid teamId, CancellationToken ct = default)
    {
        var registration = await _registrationRepository.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId, ct);

        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid? creatorId = null, CancellationToken ct = default)
    {
        // PROD-HARDEN: Service-level uniqueness check
        var existing = await _tournamentRepository.FindAsync(t => t.Name == request.Name, ct);
        if (existing.Any())
        {
            throw new ConflictException("يوجد بطولة بنفس الاسم بالفعل.");
        }

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
            // Status initialized to Draft by default
            Format = request.Format,
            MatchType = request.MatchType,
            NumberOfGroups = request.NumberOfGroups,
            WalletNumber = request.WalletNumber,
            InstaPayNumber = request.InstaPayNumber,
            IsHomeAwayEnabled = request.IsHomeAwayEnabled,
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

        if (request.Name != null && tournament.Name != request.Name) 
        {
            // PROD-HARDEN: Uniqueness Check
            var existing = await _tournamentRepository.FindAsync(t => t.Name == request.Name && t.Id != id, ct);
            if (existing.Any()) throw new ConflictException("الاسم الجديد مستخدم في بطولة أخرى.");
            tournament.Name = request.Name;
        }
        if (request.Description != null) tournament.Description = request.Description;
        if (request.Status != null && Enum.TryParse<TournamentStatus>(request.Status, true, out var newStatus)) 
            tournament.ChangeStatus(newStatus);
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
        if (request.WalletNumber != null) tournament.WalletNumber = request.WalletNumber;
        if (request.InstaPayNumber != null) tournament.InstaPayNumber = request.InstaPayNumber;
        if (request.IsHomeAwayEnabled.HasValue) tournament.IsHomeAwayEnabled = request.IsHomeAwayEnabled.Value;
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
            tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
        }
        else if (tournament.CurrentTeams < tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationClosed && !(await _matchRepository.FindAsync(m => m.TournamentId == id, ct)).Any())
        {
            // Re-open if capacity was increased and no matches exist
            if (DateTime.UtcNow <= tournament.RegistrationDeadline)
            {
                tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
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

            // 1. Check if the TEAM is already registered in THIS tournament
            if (tournament.Registrations.Any(r => r.TeamId == request.TeamId && r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn))
            {
                throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة أو قيد المراجعة.");
            }

            // 2. Check if any PLAYER in this team is already registered in THIS tournament with another team
            var teamPlayerUserIds = team.Players.Select(p => p.UserId).Where(uid => uid.HasValue).Cast<Guid>().ToList();
            if (teamPlayerUserIds.Any())
            {
                // ATOMIC OVERLAP CHECK IN DB
                var duplicatePlayerName = await _registrationRepository.GetQueryable()
                    .Where(r => r.TournamentId == tournamentId && 
                                r.TeamId != request.TeamId && 
                                r.Status != RegistrationStatus.Rejected && 
                                r.Status != RegistrationStatus.Withdrawn)
                    .SelectMany(r => r.Team!.Players)
                    .Where(p => teamPlayerUserIds.Contains(p.UserId!.Value))
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync(ct);

                if (duplicatePlayerName != null)
                {
                    throw new ConflictException($"اللاعب ({duplicatePlayerName}) مسجل بالفعل في هذه البطولة مع فريق آخر. لا يسمح للاعب بالمشاركة في نفس البطولة مع أكثر من فريق.");
                }
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
                tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
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
        
        // Check if all teams are approved and we reached max capacity 
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId, ct);
        var activeRegistrations = allRegistrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        
        if (activeRegistrations.Count == tournament.MaxTeams && activeRegistrations.All(r => r.Status == RegistrationStatus.Approved))
        {
            // STRICT MODE: Do NOT auto-generate matches.
            // Transition to WaitingForOpeningMatchSelection instead.
            if (tournament.Status != TournamentStatus.Active && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection &&
                tournament.SchedulingMode != SchedulingMode.Manual)
            {
                tournament.ChangeStatus(TournamentStatus.WaitingForOpeningMatchSelection);
                await _tournamentRepository.UpdateAsync(tournament, ct);
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
                    tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
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
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations", "Registrations.Team" }, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        ValidateOwnership(tournament, userId, userRole, ct);

        if (tournament.Status != TournamentStatus.RegistrationClosed && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
            throw new ConflictException("يجب إغلاق التسجيل قبل إنشاء المباريات.");

        if (tournament.SchedulingMode == SchedulingMode.Manual)
            throw new BadRequestException("لا يمكن استخدام التوليد التلقائي في وضع الجدولة اليدوية.");

        if (tournament.SchedulingMode == SchedulingMode.Random && !tournament.HasOpeningTeams)
            throw new ConflictException("يجب اختيار الفريقين للمباراة الافتتاحية قبل توليد المباريات في الوضع العشوائي.");

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

        // STRICT PAYMENT CHECK: Block generation if any pending payments exist
        var hasPendingPayments = tournament.Registrations.Any(r => 
            r.Status == RegistrationStatus.PendingPaymentReview || 
            r.Status == RegistrationStatus.PendingPayment);
            
        if (hasPendingPayments)
        {
            throw new ConflictException("لا يمكن توليد المباريات. بانتظار اكتمال الموافقة على جميع المدفوعات.");
        }

        var registrations = tournament.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
        int minRequired = tournament.MinTeams ?? 2;
        if (registrations.Count < minRequired)
            throw new ConflictException($"عدد الفرق غير كافٍ. المطلوب {minRequired} فريق على الأقل.");

        var teamIds = registrations.Select(r => r.TeamId).ToList();
        var matches = await CreateMatchesInternalAsync(tournament, teamIds, ct);
        
        // Change tournament status to ACTIVE after successful match generation
        tournament.ChangeStatus(TournamentStatus.Active);
        
        await _tournamentRepository.UpdateAsync(tournament, ct);
        
        var dtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
        await _notifier.SendMatchesGeneratedAsync(dtos, ct);
        
        return dtos;
    }

    public async Task<TournamentDto> CloseRegistrationAsync(Guid id, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        ValidateOwnership(tournament, userId, userRole, ct);
        
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
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

    private async Task<List<Match>> CreateMatchesInternalAsync(Tournament tournament, List<Guid> teamIds, CancellationToken ct = default)
    {
        var matches = new List<Match>();
        var random = new Random();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();
        
        if (effectiveMode == TournamentMode.GroupsKnockoutSingle || effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;
            
            var groups = new List<List<Guid>>();
            for (int i = 0; i < tournament.NumberOfGroups; i++) groups.Add(new List<Guid>());

            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remainingTeams = teamIds.Where(id => id != openingTeamA && id != openingTeamB).ToList();

                // Lock opening match to Group 1 to ensure it's Match #1 of Round 1
                int openingGroupIndex = 0; 
                groups[openingGroupIndex].Add(openingTeamA);
                groups[openingGroupIndex].Add(openingTeamB);

                var shuffledRemaining = remainingTeams.OrderBy(x => random.Next()).ToList();
                for (int i = 0; i < shuffledRemaining.Count; i++)
                {
                    int targetGroup = 0;
                    int minCount = groups[0].Count;
                    for (int g = 1; g < tournament.NumberOfGroups; g++)
                    {
                        if (groups[g].Count < minCount)
                        {
                            minCount = groups[g].Count;
                            targetGroup = g;
                        }
                    }
                    groups[targetGroup].Add(shuffledRemaining[i]);
                }
            }
            else
            {
                var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
                for (int i = 0; i < shuffledTeams.Count; i++)
                {
                    groups[i % tournament.NumberOfGroups].Add(shuffledTeams[i]);
                }
            }
            
            // Persistent Group Assignment
            for (int g = 0; g < groups.Count; g++)
            {
                foreach (var teamId in groups[g])
                {
                    var reg = tournament.Registrations.FirstOrDefault(r => r.TeamId == teamId);
                    if (reg != null) reg.GroupId = g + 1;
                }
            }

            int dayOffset = 0;
            bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                bool isOpeningGroup = tournament.HasOpeningTeams && 
                    groupTeams.Contains(tournament.OpeningTeamAId!.Value) && 
                    groupTeams.Contains(tournament.OpeningTeamBId!.Value);

                var groupMatchList = new List<Match>();
                
                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                         var match = CreateGroupMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage");
                         
                         if (isOpeningGroup && IsOpeningPair(tournament, groupTeams[i], groupTeams[j]))
                         {
                             match.IsOpeningMatch = true;
                         }
                         
                         groupMatchList.Add(match);
                         dayOffset++;
                         
                         if (isHomeAway)
                         {
                             groupMatchList.Add(CreateGroupMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                             dayOffset++;
                         }
                    }
                }

                if (isOpeningGroup)
                {
                    var openingMatch = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
                    if (openingMatch != null)
                    {
                        groupMatchList.Remove(openingMatch);
                        if (groupMatchList.Count > 0)
                        {
                            var earliestDate = groupMatchList.Min(m => m.Date);
                            var openingOrigDate = openingMatch.Date;
                            var firstMatch = groupMatchList.FirstOrDefault(m => m.Date == earliestDate);
                            if (firstMatch != null && earliestDate < openingOrigDate)
                            {
                                firstMatch.Date = openingOrigDate;
                                openingMatch.Date = earliestDate;
                            }
                        }
                        groupMatchList.Insert(0, openingMatch);
                    }
                }
                matches.AddRange(groupMatchList);
            }
        }
        else if (effectiveMode == TournamentMode.KnockoutSingle || effectiveMode == TournamentMode.KnockoutHomeAway)
        {
            List<Guid> shuffledTeams;
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB).OrderBy(x => random.Next()).ToList();
                shuffledTeams = new List<Guid> { openingTeamA, openingTeamB };
                shuffledTeams.AddRange(remaining);
            }
            else
            {
                shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                 if (i + 1 < shuffledTeams.Count)
                 {
                     var match = CreateGroupMatch(tournament, shuffledTeams[i], shuffledTeams[i+1], matchDate.AddDays(i), null, 1, "Round 1");
                     if (tournament.HasOpeningTeams && i == 0) match.IsOpeningMatch = true;
                     matches.Add(match);
                     if (isHomeAway) matches.Add(CreateGroupMatch(tournament, shuffledTeams[i+1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1"));
                 }
            }
        }
        else // League modes
        {
            List<Guid> orderedTeams;
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB).OrderBy(x => random.Next()).ToList();
                orderedTeams = new List<Guid> { openingTeamA, openingTeamB };
                orderedTeams.AddRange(remaining);
            }
            else
            {
                orderedTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.LeagueHomeAway;
            int matchCount = 0;
            bool openingMatchSet = false;
            
            for (int i = 0; i < orderedTeams.Count; i++)
            {
                for (int j = i + 1; j < orderedTeams.Count; j++)
                {
                    var match = CreateGroupMatch(tournament, orderedTeams[i], orderedTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League");
                    if (!openingMatchSet && tournament.HasOpeningTeams && IsOpeningPair(tournament, orderedTeams[i], orderedTeams[j]))
                    {
                        match.IsOpeningMatch = true;
                        openingMatchSet = true;
                        if (matchCount > 0 && matches.Count > 0)
                        {
                            var firstDate = matches[0].Date;
                            matches[0].Date = match.Date;
                            match.Date = firstDate;
                            matches.Insert(0, match);
                        }
                        else matches.Add(match);
                    }
                    else matches.Add(match);
                    
                    matchCount++;
                    if (isHomeAway)
                    {
                        matches.Add(CreateGroupMatch(tournament, orderedTeams[j], orderedTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                        matchCount++;
                    }
                }
            }
        }
        
        await _matchRepository.AddRangeAsync(matches);
        return matches;
    }

    private bool IsOpeningPair(Tournament tournament, Guid teamId1, Guid teamId2)
    {
        if (!tournament.HasOpeningTeams) return false;
        var a = tournament.OpeningTeamAId!.Value;
        var b = tournament.OpeningTeamBId!.Value;
        return (teamId1 == a && teamId2 == b) || (teamId1 == b && teamId2 == a);
    }

    private Match CreateGroupMatch(Tournament t, Guid h, Guid a, DateTime d, int? g, int? r, string s)
    {
        return new Match
        {
            TournamentId = t.Id,
            HomeTeamId = h,
            AwayTeamId = a,
            Status = MatchStatus.Scheduled,
            Date = d,
            GroupId = g,
            RoundNumber = r,
            StageName = s,
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
        }
        // PERF-FIX: Batch update all matches in single roundtrip
        await _matchRepository.UpdateRangeAsync(matches, ct);

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
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.TOURNAMENT_ELIMINATED, new Dictionary<string, string> { { "teamName", team.Name }, { "tournamentName", tournament.Name } }, entityId: tournamentId, entityType: "tournament", ct: ct);
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
        tournament.ChangeStatus(TournamentStatus.Active);
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
        tournament.ChangeStatus(TournamentStatus.Completed);
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

        // Allow selection when registration is closed OR specifically waiting for opening match selection
        if (tournament.Status != TournamentStatus.RegistrationClosed && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
        {
            throw new ConflictException("لا يمكن تحديد مباراة الافتتاح في هذه المرحلة.");
        }

        if (homeTeamId == awayTeamId) throw new ConflictException("لا يمكن اختيار نفس الفريق للمباراة.");

        // Get registrations to validate team existence
        var regs = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved, ct);
        
        // Set teams (this uses the domain logic which handles validation)
        tournament.SetOpeningTeams(homeTeamId, awayTeamId, regs.Select(r => r.TeamId), false);
        await _tournamentRepository.UpdateAsync(tournament, ct);

        // Detect if we should auto-generate
        if (tournament.SchedulingMode == SchedulingMode.Random)
        {
            var mode = tournament.GetEffectiveMode();
            bool isPureKnockout = mode == TournamentMode.KnockoutSingle || mode == TournamentMode.KnockoutHomeAway;

            if (isPureKnockout)
            {
                // Pure Knockout starting from Round 1
                await _lifecycleService.GenerateKnockoutR1Async(tournamentId, ct);
                
                // Refresh and return generated matches
                var generatedMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId && m.GroupId == null, ct);
                return _mapper.Map<IEnumerable<MatchDto>>(generatedMatches);
            }
            else
            {
                // League (Round-Robin) or Groups+Knockout starting from Groups
                return await GenerateMatchesAsync(tournamentId, userId, userRole, ct);
            }
        }

        return new List<MatchDto>();
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
        // PERF-FIX: Batch delete all participations in single roundtrip
        await _tournamentPlayerRepository.DeleteRangeAsync(participations, ct);

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

    public async Task<IEnumerable<MatchDto>> GenerateManualGroupMatchesAsync(Guid tournamentId, Guid userId, string userRole, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations" }, ct);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        ValidateOwnership(tournament, userId, userRole, ct);

        if (tournament.SchedulingMode != SchedulingMode.Manual)
            throw new BadRequestException("البطولة ليست في وضع الجدولة اليدوية.");

        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

        var registrations = tournament.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
        if (registrations.Any(r => r.GroupId == null))
            throw new BadRequestException("لم يتم تعيين جميع الفرق للمجموعات.");

        var groups = registrations.GroupBy(r => r.GroupId!.Value).ToList();
        var matches = new List<Match>();
        var matchDate = tournament.StartDate.AddHours(18);
        bool isHomeAway = tournament.GetEffectiveMode() == TournamentMode.GroupsKnockoutHomeAway || tournament.GetEffectiveMode() == TournamentMode.LeagueHomeAway;

        foreach (var group in groups)
        {
            var teamIds = group.Select(r => r.TeamId).ToList();
            var groupMatchList = new List<Match>();
            
            for (int i = 0; i < teamIds.Count; i++)
            {
                for (int j = i + 1; j < teamIds.Count; j++)
                {
                    var match = CreateGroupMatch(tournament, teamIds[i], teamIds[j], matchDate, group.Key, 1, "Group Stage");
                    if (IsOpeningPair(tournament, teamIds[i], teamIds[j])) match.IsOpeningMatch = true;
                    groupMatchList.Add(match);
                    matchDate = matchDate.AddHours(2);

                    if (isHomeAway)
                    {
                        groupMatchList.Add(CreateGroupMatch(tournament, teamIds[j], teamIds[i], matchDate.AddDays(2), group.Key, 1, "Group Stage"));
                    }
                }
            }

            // Reorder: Opening match first in its group
            var opening = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
            if (opening != null)
            {
                groupMatchList.Remove(opening);
                if (groupMatchList.Count > 0)
                {
                    var firstDate = groupMatchList.Min(m => m.Date);
                    var openingDate = opening.Date;
                    var firstMatch = groupMatchList.FirstOrDefault(m => m.Date == firstDate);
                    if (firstMatch != null) { firstMatch.Date = openingDate; opening.Date = firstDate; }
                }
                groupMatchList.Insert(0, opening);
            }
            matches.AddRange(groupMatchList);
        }

        await _matchRepository.AddRangeAsync(matches);
        
        // Automatically start the tournament after successful manual draw
        tournament.ChangeStatus(TournamentStatus.Active);
        await _tournamentRepository.UpdateAsync(tournament, ct);
        
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
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
                        matches.Add(CreateGroupMatch(tournament, teams[i], teams[j], matchDate, group.GroupId, 1, "Group Stage"));
                        matchDate = matchDate.AddHours(2);
                        if (isHomeAway)
                        {
                            matches.Add(CreateGroupMatch(tournament, teams[j], teams[i], matchDate, group.GroupId, 1, "Group Stage"));
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
                matches.Add(CreateGroupMatch(tournament, pairing.HomeTeamId, pairing.AwayTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName));
                matchDate = matchDate.AddHours(2);
                if (isHomeAway)
                {
                    matches.Add(CreateGroupMatch(tournament, pairing.AwayTeamId, pairing.HomeTeamId, matchDate, null, pairing.RoundNumber, pairing.StageName));
                    matchDate = matchDate.AddHours(2);
                }
            }
        }

        await _matchRepository.AddRangeAsync(matches);
        
        // Automatically start the tournament after successful manual draw
        tournament.ChangeStatus(TournamentStatus.Active);
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
        var openTournaments = await _tournamentRepository.FindAsync(t => t.Status == TournamentStatus.RegistrationOpen && t.RegistrationDeadline < DateTime.UtcNow, new[] { "Registrations" }, ct);
        foreach (var t in openTournaments)
        {
             t.ChangeStatus(TournamentStatus.RegistrationClosed);
             await _tournamentRepository.UpdateAsync(t, ct);
        }
        
        // 2. Start Tournament for Scheduled Start Dates
        var readyTournaments = await _tournamentRepository.FindAsync(t => t.Status == TournamentStatus.RegistrationClosed && t.StartDate <= DateTime.UtcNow, new[] { "Registrations" }, ct);
        foreach (var t in readyTournaments)
        {
             // Auto-generate matches if needed and mode is Random
             if (t.SchedulingMode == SchedulingMode.Random && !(await _matchRepository.FindAsync(m => m.TournamentId == t.Id, ct)).Any())
             {
                 // Check minimum teams
                 var registrations = t.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
                 if (registrations.Count >= (t.MinTeams ?? 2) && t.HasOpeningTeams)
                 {
                     var teamIds = registrations.Select(r => r.TeamId).ToList();
                     await CreateMatchesInternalAsync(t, teamIds, ct);
                 }
                 else
                 {
                     continue; // Skip auto-start if not ready
                 }
             }

             t.ChangeStatus(TournamentStatus.Active);
             await _tournamentRepository.UpdateAsync(t, ct);
        }
    }
}

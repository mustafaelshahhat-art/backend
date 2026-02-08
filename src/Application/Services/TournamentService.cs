using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.Interfaces;
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

    public async Task<IEnumerable<TournamentDto>> GetAllAsync()
    {
        var tournaments = await _tournamentRepository.GetAllAsync(new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain", "WinnerTeam" });
        return _mapper.Map<IEnumerable<TournamentDto>>(tournaments);
    }

    public async Task<TournamentDto?> GetByIdAsync(Guid id)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain", "WinnerTeam" });
        return tournament == null ? null : _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> CreateAsync(CreateTournamentRequest request)
    {
        var tournament = new Tournament
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RegistrationDeadline = request.RegistrationDeadline,
            EntryFee = request.EntryFee,
            MaxTeams = request.MaxTeams,
            Location = request.Location,
            Rules = request.Rules,
            Prizes = request.Prizes,
            Status = "registration_open"
        };

        await _tournamentRepository.AddAsync(tournament);
        await _analyticsService.LogActivityAsync("إنشاء بطولة", $"تم إنشاء بطولة {tournament.Name}.", null, "Admin");
        
        // Real-time Event
        await _notifier.SendTournamentCreatedAsync(_mapper.Map<TournamentDto>(tournament));
        
        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> UpdateAsync(Guid id, UpdateTournamentRequest request)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

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

        await _tournamentRepository.UpdateAsync(tournament);

        // Real-time Event
        await _notifier.SendTournamentUpdatedAsync(_mapper.Map<TournamentDto>(tournament));

        return _mapper.Map<TournamentDto>(tournament);
    }

    public async Task<TournamentDto> CloseRegistrationAsync(Guid id)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), id);

        tournament.Status = "registration_closed";
        
        await _tournamentRepository.UpdateAsync(tournament);
        await _analyticsService.LogActivityAsync("إغلاق التسجيل", $"تم إغلاق التسجيل في بطولة {tournament.Name}", null, "Admin");
        
        var dto = _mapper.Map<TournamentDto>(tournament);
        
        // Real-time Event - Emit AFTER DB commit
        await _notifier.SendTournamentUpdatedAsync(dto);
        
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(id);
        if (tournament == null) return;

        // 1. Clean up Matches
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == id);
        foreach (var match in matches)
        {
            await _matchRepository.DeleteAsync(match);
        }

        // 2. Clean up Registrations
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == id);
        foreach (var reg in registrations)
        {
            await _registrationRepository.DeleteAsync(reg);
        }

        // 3. Delete Tournament
        await _tournamentRepository.DeleteAsync(tournament);
        
        await _analyticsService.LogActivityAsync("حذف بطولة", $"تم حذف بطولة {tournament.Name} (ID: {id}).", null, "Admin");
        
        // Real-time Event
        await _notifier.SendTournamentDeletedAsync(id);
    }

    public async Task<TeamRegistrationDto> RegisterTeamAsync(Guid tournamentId, RegisterTeamRequest request, Guid userId)
    {
        // 1. Verify Tournament Status
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        
        if (tournament.Status != "registration_open")
        {
            throw new BadRequestException("التسجيل في هذه البطولة مغلق حالياً.");
        }

        // 2. Verify Team Ownership
        var team = await _teamRepository.GetByIdAsync(request.TeamId);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);
        
        if (!team.IsActive)
        {
            throw new ForbiddenException("لا يمكن للفرق المعطلة التسجيل في البطولات.");
        }
        
        if (team.CaptainId != userId)
        {
            throw new ForbiddenException("فقط صاحب الفريق (الرئيس) يمكنه تسجيل الفريق في البطولات.");
        }

        // 3. Check if team is already registered in THIS tournament
        var existing = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == request.TeamId);
        if (existing.Any()) throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة.");

        // 4. Check if team is registered in ANY other active tournament
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TeamId == request.TeamId);
        foreach (var reg in allRegistrations)
        {
            if (reg.Status == RegistrationStatus.Rejected) continue;

            var t = await _tournamentRepository.GetByIdAsync(reg.TournamentId);
            if (t != null && t.Status != "completed" && t.Status != "cancelled")
            {
                throw new ConflictException($"الفريق مسجل بالفعل في بطولة أخرى جارية: {t.Name}");
            }
        }

        // 5. Check capacity
        var regCount = (await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.Status != RegistrationStatus.Rejected)).Count();
        if (regCount >= tournament.MaxTeams) throw new ConflictException("عذراً، اكتمل العدد الأقصى للفرق في هذه البطولة.");

        var registration = new TeamRegistration
        {
            TournamentId = tournamentId,
            TeamId = request.TeamId,
            Status = RegistrationStatus.PendingPaymentReview
        };

        await _registrationRepository.AddAsync(registration);
        
        tournament.CurrentTeams++;
        await _tournamentRepository.UpdateAsync(tournament);
        
        return _mapper.Map<TeamRegistrationDto>(registration);
    }

    public async Task<IEnumerable<TeamRegistrationDto>> GetRegistrationsAsync(Guid tournamentId)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId);
        return _mapper.Map<IEnumerable<TeamRegistrationDto>>(registrations);
    }

    public async Task<TeamRegistrationDto> SubmitPaymentAsync(Guid tournamentId, Guid teamId, SubmitPaymentRequest request, Guid userId)
    {
        // 1. Verify Tournament Status
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);
        
        if (tournament.Status != "registration_open")
        {
            throw new BadRequestException("لا يمكن إرسال إيصالات لبطولة مغلقة أو جارية.");
        }

        // 2. Verify Team Ownership
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);
        
        if (team.CaptainId != userId)
        {
            throw new ForbiddenException("فقط صاحب الفريق يمكنه إرسال إيصال الدفع.");
        }

        // 3. Verify Registration Status
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("طلب التسجيل غير موجود.");

        if (reg.Status != RegistrationStatus.PendingPaymentReview)
        {
            throw new BadRequestException("لا يمكن تعديل إيصال الدفع بعد الموافقة عليه أو رفضه.");
        }

        reg.PaymentReceiptUrl = request.PaymentReceiptUrl;
        reg.SenderNumber = request.SenderNumber;
        
        await _registrationRepository.UpdateAsync(reg);
        
        // Notify Admins
        await _notificationService.SendNotificationAsync(Guid.Empty, "طلب دفع جديد للمراجعة", $"تم تقديم إيصال دفع جديد لفريق {team.Name} في بطولة {tournament.Name}", "admin_broadcast");
        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    public async Task<TeamRegistrationDto> ApproveRegistrationAsync(Guid tournamentId, Guid teamId)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("Registration not found.");

        reg.Status = RegistrationStatus.Approved;
        
        await _registrationRepository.UpdateAsync(reg);
        await _analyticsService.LogActivityAsync("قبول طلب تسجيل", $"تمت الموافقة على انضمام فريق ID {teamId} لبطولة ID {tournamentId}", null, "Admin");
        
        // Notify Captain
        var team = await _teamRepository.GetByIdAsync(teamId);
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain", "WinnerTeam" });
        if (team != null) 
        {
            await _notificationService.SendNotificationAsync(team.CaptainId, "تمت الموافقة على التسجيل", $"تمت الموافقة على تسجيلكم في بطولة {tournament?.Name}.", "system");
            
            // Lightweight System Event for backward compatibility
            await _notifier.SendSystemEventAsync("PAYMENT_APPROVED", new { TournamentId = tournamentId, TeamId = teamId }, $"user:{team.CaptainId}");
        }

        // Real-time Event - Emit FULL Tournament with updated registrations
        if (tournament != null)
        {
            var tournamentDto = _mapper.Map<TournamentDto>(tournament);
            await _notifier.SendRegistrationApprovedAsync(tournamentDto);
        }

        // Check if tournament is now full with approved teams - auto-generate matches
        await TryGenerateMatchesIfFullAsync(tournamentId);

        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    private async Task TryGenerateMatchesIfFullAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) return;

        // Get approved registrations only
        var approvedRegs = await _registrationRepository.FindAsync(r => 
            r.TournamentId == tournamentId && r.Status == RegistrationStatus.Approved);
        
        var approvedCount = approvedRegs.Count();
        
        // Check if we have reached max teams
        if (approvedCount < tournament.MaxTeams) return;

        // Check if matches already exist
        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (existingMatches.Any()) return;

        // Generate Round Robin matches
        var teamIds = approvedRegs.Select(r => r.TeamId).ToList();
        var random = new Random();
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();

        var matchDate = DateTime.UtcNow.AddDays(7);
        var matchNumber = 0;

        for (int i = 0; i < shuffledTeams.Count; i++)
        {
            for (int j = i + 1; j < shuffledTeams.Count; j++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    HomeTeamId = shuffledTeams[i],
                    AwayTeamId = shuffledTeams[j],
                    Status = MatchStatus.Scheduled,
                    Date = matchDate.AddDays(matchNumber * 3),
                    HomeScore = 0,
                    AwayScore = 0
                };
                
                await _matchRepository.AddAsync(match);
                matchNumber++;
            }
        }

        // Update tournament status to Active
        tournament.Status = "active";
        await _tournamentRepository.UpdateAsync(tournament);

        await _analyticsService.LogActivityAsync("توليد مباريات تلقائي", $"تم توليد {matchNumber} مباراة لبطولة {tournament.Name}", null, "System");
        
        // Notify all captains
        foreach (var reg in approvedRegs)
        {
            var t = await _teamRepository.GetByIdAsync(reg.TeamId);
            if (t != null)
            {
                await _notificationService.SendNotificationAsync(t.CaptainId, "جدول المباريات جاهز!", $"تم توليد جدول المباريات لبطولة {tournament.Name}. تحقق من المباريات القادمة.", "tournament");
            }
        }
    }

    public async Task<TeamRegistrationDto> RejectRegistrationAsync(Guid tournamentId, Guid teamId, RejectRegistrationRequest request)
    {
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && r.TeamId == teamId);
        var reg = registrations.FirstOrDefault();
        if (reg == null) throw new NotFoundException("Registration not found.");

        reg.Status = RegistrationStatus.Rejected;
        reg.RejectionReason = request.Reason;

        await _registrationRepository.UpdateAsync(reg);
        
        // Decrement capacity
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, new[] { "Registrations", "Registrations.Team", "Registrations.Team.Captain", "WinnerTeam" });
        if (tournament != null)
        {
            tournament.CurrentTeams = Math.Max(0, tournament.CurrentTeams - 1);
            await _tournamentRepository.UpdateAsync(tournament);
        }
        
        // Notify Captain
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team != null) 
        {
            await _notificationService.SendNotificationAsync(team.CaptainId, "تم رفض التسجيل", $"تم رفض تسجيلكم في بطولة {tournament?.Name}: {request.Reason}", "system");
            
            // Lightweight System Event for backward compatibility
            await _notifier.SendSystemEventAsync("PAYMENT_REJECTED", new { TournamentId = tournamentId, TeamId = teamId, Reason = request.Reason }, $"user:{team.CaptainId}");
        }

        // Real-time Event - Emit FULL Tournament with updated registrations
        if (tournament != null)
        {
            var tournamentDto = _mapper.Map<TournamentDto>(tournament);
            await _notifier.SendRegistrationRejectedAsync(tournamentDto);
        }

        return _mapper.Map<TeamRegistrationDto>(reg);
    }

    public async Task<IEnumerable<PendingPaymentResponse>> GetPendingPaymentsAsync()
    {
        var pending = await _registrationRepository.FindAsync(
            r => r.Status == RegistrationStatus.PendingPaymentReview,
            new[] { "Team", "Team.Captain", "Tournament" });
        
        var result = new List<PendingPaymentResponse>();
        foreach (var p in pending)
        {
            if (p.Tournament == null) continue;
            
            result.Add(new PendingPaymentResponse
            {
                Registration = _mapper.Map<TeamRegistrationDto>(p),
                Tournament = _mapper.Map<TournamentDto>(p.Tournament)
            });
        }
        return result;
    }

    public async Task<IEnumerable<PendingPaymentResponse>> GetAllPaymentRequestsAsync()
    {
        // Return all payment requests with receipts (pending, approved, rejected)
        var registrations = await _registrationRepository.FindAsync(
            r => r.Status == RegistrationStatus.PendingPaymentReview || 
                 r.Status == RegistrationStatus.Approved || 
                 r.Status == RegistrationStatus.Rejected,
            new[] { "Team", "Team.Captain", "Tournament" });
        
        var result = new List<PendingPaymentResponse>();
        foreach (var reg in registrations)
        {
            if (reg.Tournament == null) continue;
            
            result.Add(new PendingPaymentResponse
            {
                Registration = _mapper.Map<TeamRegistrationDto>(reg),
                Tournament = _mapper.Map<TournamentDto>(reg.Tournament)
            });
        }
        return result;
    }

    public async Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), tournamentId);

        // Check if matches already exist
        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (existingMatches.Any())
        {
            throw new ConflictException("المباريات موجودة بالفعل لهذه البطولة.");
        }

        // Get all non-rejected registrations (approved or pending review - they count as registered)
        var registrations = await _registrationRepository.FindAsync(r => 
            r.TournamentId == tournamentId && r.Status != RegistrationStatus.Rejected);
        
        var teamIds = registrations.Select(r => r.TeamId).ToList();

        if (teamIds.Count < 2)
        {
            throw new BadRequestException("يجب وجود فريقين على الأقل لتوليد المباريات.");
        }

        // Generate Round Robin matches
        var random = new Random();
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();

        var matchDate = DateTime.UtcNow.AddDays(7);
        var matchNumber = 0;
        var matches = new List<Match>();

        for (int i = 0; i < shuffledTeams.Count; i++)
        {
            for (int j = i + 1; j < shuffledTeams.Count; j++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    HomeTeamId = shuffledTeams[i],
                    AwayTeamId = shuffledTeams[j],
                    Status = MatchStatus.Scheduled,
                    Date = matchDate.AddDays(matchNumber * 3),
                    HomeScore = 0,
                    AwayScore = 0
                };
                
                await _matchRepository.AddAsync(match);
                matches.Add(match);
                matchNumber++;
            }
        }

        // Update tournament status to Active
        tournament.Status = "active";
        await _tournamentRepository.UpdateAsync(tournament);

        await _analyticsService.LogActivityAsync("توليد مباريات", $"تم توليد {matchNumber} مباراة لبطولة {tournament.Name}", null, "Admin");

        var matchDtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
        await _notifier.SendMatchesGeneratedAsync(matchDtos);

        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<IEnumerable<TournamentStandingDto>> GetStandingsAsync(Guid tournamentId)
    {
        // 1. Get all matches for tournament
        var matches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId && m.Status == MatchStatus.Finished);
        
        // 2. Get all approved or withdrawn registrations (teams that participated)
        var registrations = await _registrationRepository.FindAsync(
            r => r.TournamentId == tournamentId && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Withdrawn),
            new[] { "Team" }
        );
        
        // 3. Initialize standings
        var standings = new List<TournamentStandingDto>();
        
        foreach (var reg in registrations)
        {
            standings.Add(new TournamentStandingDto
            {
                TeamId = reg.TeamId,
                TeamName = reg.Team?.Name ?? "Unknown",
                TeamLogoUrl = reg.Team?.Logo,
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

        // 4. Calculate stats
        foreach (var match in matches)
        {
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

        // 5. Sort by Points desc, then Goal Difference desc, then Goals For desc
        return standings
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ToList();
    }
}

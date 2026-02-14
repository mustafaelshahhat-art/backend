using Application.Common;
using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Commands.CreateTournament;

public class CreateTournamentCommandHandler : IRequestHandler<CreateTournamentCommand, TournamentDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly IRealTimeNotifier _notifier;

    public CreateTournamentCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notifier = notifier;
    }

    public async Task<TournamentDto> Handle(CreateTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = new Tournament
        {
            Name = request.Request.Name,
            Description = request.Request.Description,
            CreatorUserId = request.CreatorUserId,
            StartDate = request.Request.StartDate,
            EndDate = request.Request.EndDate,
            RegistrationDeadline = request.Request.RegistrationDeadline,
            EntryFee = request.Request.EntryFee,
            MaxTeams = request.Request.MaxTeams,
            Location = request.Request.Location,
            Rules = request.Request.Rules,
            Prizes = request.Request.Prizes,
            Format = request.Request.Format,
            MatchType = request.Request.MatchType,
            NumberOfGroups = request.Request.NumberOfGroups,
            QualifiedTeamsPerGroup = request.Request.QualifiedTeamsPerGroup,
            WalletNumber = request.Request.WalletNumber,
            InstaPayNumber = request.Request.InstaPayNumber,
            IsHomeAwayEnabled = request.Request.IsHomeAwayEnabled,
            SeedingMode = request.Request.SeedingMode,
            PaymentMethodsJson = request.Request.PaymentMethodsJson,
            Mode = request.Request.Mode,
            AllowLateRegistration = request.Request.AllowLateRegistration,
            LateRegistrationMode = request.Request.LateRegistrationMode,
            SchedulingMode = request.Request.SchedulingMode
        };

        // PROD-HARDEN: Status is initialized to Draft by default in entity. Request cannot override it.

        if (request.Request.Mode.HasValue)
        {
            (tournament.Format, tournament.MatchType) = MapModeToLegacy(request.Request.Mode.Value);
        }

        // PROD-FIX: Business Rule Validations
        ValidateTournamentRules(tournament);

        // PROD-FIX: Name Uniqueness Check
        var exists = await _tournamentRepository.AnyAsync(t => t.Name == tournament.Name, cancellationToken);
        if (exists)
        {
            throw new Shared.Exceptions.BadRequestException("اسم البطولة مستخدم بالفعل. يرجى اختيار اسم آخر.");
        }

        await _tournamentRepository.AddAsync(tournament, cancellationToken);
        
        // PROD-FIX: Fix Audit Log User Context
        // Ideally we'd have a current user service, but we use the command's CreatorUserId
        var creatorName = "منشئ البطولة"; // Fallback or fetch from user repo if needed. For now, avoid "Admin" hardcode if creatorId exists.
        
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TOURNAMENT_CREATED, 
            new Dictionary<string, string> { { "tournamentName", tournament.Name } }, 
            request.CreatorUserId, 
            creatorName,
            cancellationToken);

        var dto = _mapper.Map<TournamentDto>(tournament);
        await _notifier.SendTournamentCreatedAsync(dto, cancellationToken);

        return dto;
    }

    private void ValidateTournamentRules(Tournament t)
    {
        // 1. Group Logic
        if (t.Format == TournamentFormat.GroupsThenKnockout || t.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            if (t.NumberOfGroups <= 0) 
                throw new Shared.Exceptions.BadRequestException("يجب تحديد عدد المجموعات في هذا النوع من البطولات.");
            
            if (t.QualifiedTeamsPerGroup <= 0)
                throw new Shared.Exceptions.BadRequestException("يجب تحديد عدد المتأهلين من كل مجموعة.");

            if (t.MaxTeams < t.NumberOfGroups * 2) // Basic heuristic: min 2 teams per group
                throw new Shared.Exceptions.BadRequestException("عدد الفرق الحد الأقصى لا يكفي لعدد المجموعات المحدد.");
        }

        // 2. Knockout Power of 2 (if it's purely knockout or for the qualified teams)
        if (t.Format == TournamentFormat.KnockoutOnly)
        {
            if ((t.MaxTeams & (t.MaxTeams - 1)) != 0 || t.MaxTeams < 2)
                throw new Shared.Exceptions.BadRequestException("في نظام خروج المغلوب، يجب أن يكون عدد الفرق من مضاعفات الرقم 2 (2, 4, 8, 16, 32...).");
        }

        // 3. Date Integrity (redundant but safe)
        if (t.RegistrationDeadline > t.StartDate)
            throw new Shared.Exceptions.BadRequestException("آخر موعد للتسجيل لا يمكن أن يكون بعد تاريخ بداية البطولة.");
        
        if (t.StartDate >= t.EndDate)
             throw new Shared.Exceptions.BadRequestException("تاريخ نهاية البطولة يجب أن يكون بعد تاريخ البداية.");

        // 4. Late Registration
        if (t.AllowLateRegistration && t.LateRegistrationMode == LateRegistrationMode.None)
             throw new Shared.Exceptions.BadRequestException("يرجى تحديد نمط التسجيل المتأخر.");
        
        if (!t.AllowLateRegistration && t.LateRegistrationMode != LateRegistrationMode.None)
             t.LateRegistrationMode = LateRegistrationMode.None; // Auto-correct
    }

    private (TournamentFormat format, TournamentLegType legType) MapModeToLegacy(TournamentMode mode)
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
}

using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.UpdateTournament;

public class UpdateTournamentCommandHandler : IRequestHandler<UpdateTournamentCommand, TournamentDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;

    public UpdateTournamentCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<TournamentDto> Handle(UpdateTournamentCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.Id, new[] { "Registrations", "WinnerTeam" }, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.Id);

        // Authorization
        if (request.UserRole != UserRole.Admin.ToString() && tournament.CreatorUserId != request.UserId)
        {
            throw new ForbiddenException("غير مصرح لك بتعديل هذه البطولة.");
        }

        // Safety Check: Format Change
        if (request.Request.Format.HasValue && request.Request.Format.Value != tournament.Format)
        {
            var matches = await _matchRepository.FindAsync(m => m.TournamentId == request.Id, cancellationToken);
            var hasActiveKnockout = matches.Any(m => m.GroupId == null && m.StageName != "League" && m.Status != MatchStatus.Scheduled);
            
            if (hasActiveKnockout)
            {
                throw new ConflictException("لا يمكن تغيير نظام البطولة بعد بدء الأدوار الإقصائية.");
            }
        }

        if (request.Request.Name != null) tournament.Name = request.Request.Name;
        if (request.Request.Description != null) tournament.Description = request.Request.Description;
        
        if (request.Request.Status != null && Enum.TryParse<TournamentStatus>(request.Request.Status, true, out var newStatus)) 
        {
            if (tournament.Status != newStatus)
            {
                // PROD-HARDEN: Using ChangeStatus for state machine enforcement
                tournament.ChangeStatus(newStatus);
            }
        }

        if (request.Request.StartDate.HasValue) tournament.StartDate = request.Request.StartDate.Value;
        if (request.Request.EndDate.HasValue) tournament.EndDate = request.Request.EndDate.Value;
        if (request.Request.RegistrationDeadline.HasValue) tournament.RegistrationDeadline = request.Request.RegistrationDeadline.Value;
        if (request.Request.EntryFee.HasValue) tournament.EntryFee = request.Request.EntryFee.Value;
        if (request.Request.MaxTeams.HasValue) tournament.MaxTeams = request.Request.MaxTeams.Value;
        if (request.Request.Location != null) tournament.Location = request.Request.Location;
        if (request.Request.Rules != null) tournament.Rules = request.Request.Rules;
        if (request.Request.Prizes != null) tournament.Prizes = request.Request.Prizes;
        if (request.Request.Format.HasValue) tournament.Format = request.Request.Format.Value;
        if (request.Request.MatchType.HasValue) tournament.MatchType = request.Request.MatchType.Value;
        if (request.Request.NumberOfGroups.HasValue) tournament.NumberOfGroups = request.Request.NumberOfGroups.Value;
        if (request.Request.QualifiedTeamsPerGroup.HasValue) tournament.QualifiedTeamsPerGroup = request.Request.QualifiedTeamsPerGroup.Value;
        if (request.Request.WalletNumber != null) tournament.WalletNumber = request.Request.WalletNumber;
        if (request.Request.InstaPayNumber != null) tournament.InstaPayNumber = request.Request.InstaPayNumber;
        if (request.Request.IsHomeAwayEnabled.HasValue) tournament.IsHomeAwayEnabled = request.Request.IsHomeAwayEnabled.Value;
        if (request.Request.SeedingMode.HasValue) tournament.SeedingMode = request.Request.SeedingMode.Value;
        if (request.Request.PaymentMethodsJson != null) tournament.PaymentMethodsJson = request.Request.PaymentMethodsJson;
        
        if (request.Request.Mode.HasValue) 
        {
            tournament.Mode = request.Request.Mode.Value;
            var (format, matchType) = MapModeToLegacy(request.Request.Mode.Value);
            tournament.Format = format;
            tournament.MatchType = matchType;
        }
        
        if (request.Request.OpeningMatchId.HasValue) tournament.OpeningMatchId = request.Request.OpeningMatchId.Value;
        if (request.Request.AllowLateRegistration.HasValue) tournament.AllowLateRegistration = request.Request.AllowLateRegistration.Value;
        if (request.Request.LateRegistrationMode.HasValue) tournament.LateRegistrationMode = request.Request.LateRegistrationMode.Value;
        if (request.Request.OpeningMatchHomeTeamId.HasValue) tournament.OpeningMatchHomeTeamId = request.Request.OpeningMatchHomeTeamId.Value;
        if (request.Request.OpeningMatchAwayTeamId.HasValue) tournament.OpeningMatchAwayTeamId = request.Request.OpeningMatchAwayTeamId.Value;

        // Force auto-closure if capacity reached after manual update
        if (tournament.CurrentTeams >= tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationOpen)
        {
            tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
        }
        else if (tournament.CurrentTeams < tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationClosed)
        {
            var matches = await _matchRepository.FindAsync(m => m.TournamentId == request.Id, cancellationToken);
            if (!matches.Any() && DateTime.UtcNow <= tournament.RegistrationDeadline)
            {
                tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
            }
        }

        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

        var dto = _mapper.Map<TournamentDto>(tournament);
        await _notifier.SendTournamentUpdatedAsync(dto, cancellationToken);

        return dto;
    }

    private (TournamentFormat Format, TournamentLegType MatchType) MapModeToLegacy(TournamentMode mode)
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

using Application.DTOs.Tournaments;
using Application.Features.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Tournaments.Queries.GetTournamentById;

/// <summary>
/// PERF: Replaced AutoMapper with direct DTO construction.
/// Eliminates per-entity reflection overhead and redundant navigation-property
/// evaluation in the AutoMapper profile (Registrations.Where, WinnerTeam.Name)
/// that always resolved to null/empty on AsNoTracking projections, only to be
/// overwritten immediately after mapping.
/// </summary>
public class GetTournamentByIdHandler : IRequestHandler<GetTournamentByIdQuery, TournamentDto?>
{
    private readonly IRepository<Tournament> _tournamentRepository;

    public GetTournamentByIdHandler(IRepository<Tournament> tournamentRepository)
        => _tournamentRepository = tournamentRepository;

    public async Task<TournamentDto?> Handle(
        GetTournamentByIdQuery request,
        CancellationToken ct)
    {
        var id = request.TournamentId;
        var userId = request.CurrentUserId;
        var userRole = request.CurrentUserRole;

        var item = await _tournamentRepository.ExecuteFirstOrDefaultAsync(
            _tournamentRepository.GetQueryable()
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
                        CaptainName = r.Team != null && r.Team.Players != null
                            ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty
                            : string.Empty
                    }).ToList()
            }), ct);

        if (item == null) return null;

        // PRIVACY: Privacy filter for Drafts
        if (item.Tournament.Status == TournamentStatus.Draft && item.Tournament.CreatorUserId != userId && userRole != "Admin")
        {
            return null;
        }

        var t = item.Tournament;
        var dto = new TournamentDto
        {
            Id = t.Id,
            Name = t.Name,
            NameAr = t.NameAr,
            NameEn = t.NameEn,
            CreatorUserId = t.CreatorUserId,
            ImageUrl = t.ImageUrl,
            Status = t.Status.ToString(),
            Mode = t.GetEffectiveMode(),
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            RegistrationDeadline = t.RegistrationDeadline,
            EntryFee = t.EntryFee,
            MaxTeams = t.MaxTeams,
            MinTeams = t.MinTeams,
            CurrentTeams = t.CurrentTeams,
            Location = t.Location,
            Description = t.Description,
            Rules = t.Rules,
            Prizes = t.Prizes,
            Format = t.Format.ToString(),
            MatchType = t.MatchType.ToString(),
            NumberOfGroups = t.NumberOfGroups,
            WalletNumber = t.WalletNumber,
            InstaPayNumber = t.InstaPayNumber,
            IsHomeAwayEnabled = t.IsHomeAwayEnabled,
            PaymentMethodsJson = t.PaymentMethodsJson,
            WinnerTeamId = t.WinnerTeamId,
            WinnerTeamName = item.WinnerTeamName,
            AllowLateRegistration = t.AllowLateRegistration,
            LateRegistrationMode = t.LateRegistrationMode,
            SchedulingMode = t.SchedulingMode,
            OpeningMatchHomeTeamId = t.OpeningMatchHomeTeamId,
            OpeningMatchAwayTeamId = t.OpeningMatchAwayTeamId,
            OpeningMatchId = t.OpeningMatchId,
            AdminId = t.CreatorUserId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Registrations = item.Registrations.Select(r =>
            {
                var reg = r.Registration;
                return new TeamRegistrationDto
                {
                    Id = reg.Id,
                    TeamId = reg.TeamId,
                    TournamentId = reg.TournamentId,
                    Status = reg.Status.ToString(),
                    PaymentReceiptUrl = reg.PaymentReceiptUrl,
                    SenderNumber = reg.SenderNumber,
                    RejectionReason = reg.RejectionReason,
                    PaymentMethod = reg.PaymentMethod,
                    RegisteredAt = reg.CreatedAt,
                    IsQualifiedForKnockout = reg.IsQualifiedForKnockout,
                    TeamName = r.TeamName,
                    CaptainName = r.CaptainName,
                };
            }).ToList(),
            RequiresAdminIntervention = TournamentHelper.CheckInterventionRequired(t,
                item.TotalMatches,
                item.FinishedMatches,
                item.TotalRegs,
                item.ApprovedRegs,
                DateTime.UtcNow)
        };

        return dto;
    }
}

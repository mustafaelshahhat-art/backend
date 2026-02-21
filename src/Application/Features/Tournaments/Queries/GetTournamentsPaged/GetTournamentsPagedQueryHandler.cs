using Application.Common.Models;
using Application.DTOs.Tournaments;
using Application.Features.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetTournamentsPaged;

/// <summary>
/// PERF: Replaced AutoMapper with direct DTO construction.
/// AutoMapper was: materializing full entity → reflection-mapping every property
/// → accessing nav-prop expressions (Registrations, WinnerTeam) that resolve to
/// null/empty on AsNoTracking projections → then handlers overwrite those fields.
/// Direct construction eliminates reflection overhead and redundant nav-prop evaluation.
/// </summary>
public class GetTournamentsPagedQueryHandler : IRequestHandler<GetTournamentsPagedQuery, PagedResult<TournamentDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<User> _userRepository;

    public GetTournamentsPagedQueryHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<User> userRepository)
    {
        _tournamentRepository = tournamentRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResult<TournamentDto>> Handle(GetTournamentsPagedQuery request, CancellationToken ct)
    {
        var page = request.Page;
        var pageSize = request.PageSize;

        if (pageSize > 100) pageSize = 100;

        // Resolve the requesting user's teamId so the card can show their registration status.
        // Uses Guid.Empty as default — matches no team, resulting in null MyRegistration.
        var viewerTeamId = Guid.Empty;
        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            var userTeamId = await _userRepository.ExecuteFirstOrDefaultAsync(
                _userRepository.GetQueryable()
                    .Where(u => u.Id == request.UserId.Value)
                    .Select(u => u.TeamId), ct);
            if (userTeamId.HasValue)
                viewerTeamId = userTeamId.Value;
        }

        // Role-based filtering (moved from controller)
        Guid? creatorId = null;
        bool includeDrafts = false;

        if (request.UserRole == UserRole.TournamentCreator.ToString())
        {
            creatorId = request.UserId;
            includeDrafts = true;
        }
        else if (request.UserRole == UserRole.Admin.ToString())
        {
            includeDrafts = true;
        }

        var query = _tournamentRepository.GetQueryable();

        if (creatorId.HasValue)
        {
            query = query.Where(t => t.CreatorUserId == creatorId.Value);
        }
        else if (!includeDrafts)
        {
            query = query.Where(t => t.Status != TournamentStatus.Draft);
        }

        var totalCount = await _tournamentRepository.ExecuteCountAsync(query, ct);
        var items = await _tournamentRepository.ExecuteQueryAsync(query
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
                // Include the requesting user's team registration (at most 1 per tournament)
                // so the tournament card can display their registration status.
                // When viewerTeamId is Guid.Empty (anonymous/no team), this returns null.
                MyRegistration = t.Registrations
                    .Where(r => r.TeamId == viewerTeamId)
                    .Select(r => new
                    {
                        r.Id,
                        r.TeamId,
                        r.TournamentId,
                        r.Status,
                        r.PaymentReceiptUrl,
                        r.SenderNumber,
                        r.RejectionReason,
                        r.PaymentMethod,
                        r.CreatedAt,
                        r.IsQualifiedForKnockout,
                        TeamName = r.Team != null ? r.Team.Name : string.Empty
                    })
                    .FirstOrDefault()
            }), ct);

        var dtos = new List<TournamentDto>();
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
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
                // Include only the requesting user's team registration (for card status display).
                // All other registrations are omitted — counts suffice for the list view.
                Registrations = item.MyRegistration != null
                    ? new List<TeamRegistrationDto>
                    {
                        new TeamRegistrationDto
                        {
                            Id = item.MyRegistration.Id,
                            TeamId = item.MyRegistration.TeamId,
                            TournamentId = item.MyRegistration.TournamentId,
                            Status = item.MyRegistration.Status.ToString(),
                            PaymentReceiptUrl = item.MyRegistration.PaymentReceiptUrl,
                            SenderNumber = item.MyRegistration.SenderNumber,
                            RejectionReason = item.MyRegistration.RejectionReason,
                            PaymentMethod = item.MyRegistration.PaymentMethod,
                            RegisteredAt = item.MyRegistration.CreatedAt,
                            IsQualifiedForKnockout = item.MyRegistration.IsQualifiedForKnockout,
                            TeamName = item.MyRegistration.TeamName,
                        }
                    }
                    : new List<TeamRegistrationDto>(),
                RequiresAdminIntervention = TournamentHelper.CheckInterventionRequired(t,
                    item.TotalMatches,
                    item.FinishedMatches,
                    item.TotalRegs,
                    item.ApprovedRegs,
                    now)
            };

            dtos.Add(dto);
        }

        return new PagedResult<TournamentDto>(dtos, totalCount, page, pageSize);
    }
}

using Application.Common.Models;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetRegistrations;

public class GetRegistrationsQueryHandler : IRequestHandler<GetRegistrationsQuery, PagedResult<TeamRegistrationDto>>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;

    public GetRegistrationsQueryHandler(IRepository<TeamRegistration> registrationRepository)
    {
        _registrationRepository = registrationRepository;
    }

    public async Task<PagedResult<TeamRegistrationDto>> Handle(GetRegistrationsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize > 100 ? 100 : request.PageSize;

        // PERF-FIX: Server-side projection — previously loaded full Team + ALL Player
        // entities (all columns) for every registration via Include + AutoMapper.
        // A page of 20 registrations × 15 players = 300 Player entities materialized.
        // Now projects only the DTO fields at the SQL level.
        var query = _registrationRepository.GetQueryable()
            .Where(r => r.TournamentId == request.TournamentId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await _registrationRepository.ExecuteCountAsync(query, cancellationToken);

        var projectedQuery = query
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new TeamRegistrationDto
            {
                Id = r.Id,
                TeamId = r.TeamId,
                TeamName = r.Team != null ? r.Team.Name : string.Empty,
                CaptainName = r.Team != null
                    ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain)
                                    .Select(p => p.Name).FirstOrDefault() ?? string.Empty
                    : string.Empty,
                Status = r.Status.ToString(),
                PaymentReceiptUrl = r.PaymentReceiptUrl,
                SenderNumber = r.SenderNumber,
                RejectionReason = r.RejectionReason,
                PaymentMethod = r.PaymentMethod,
                RegisteredAt = r.CreatedAt,
                TournamentId = r.TournamentId,
                IsQualifiedForKnockout = r.IsQualifiedForKnockout
            });

        var dtos = await _registrationRepository.ExecuteQueryAsync(projectedQuery, cancellationToken);

        return new PagedResult<TeamRegistrationDto>(dtos, totalCount, request.Page, pageSize);
    }
}

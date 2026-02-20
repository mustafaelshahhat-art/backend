using Application.Common.Models;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamFinancials;

public class GetTeamFinancialsQueryHandler : IRequestHandler<GetTeamFinancialsQuery, PagedResult<TeamRegistrationDto>>
{
    private readonly IRepository<TeamRegistration> _registrationRepository;
    public GetTeamFinancialsQueryHandler(IRepository<TeamRegistration> registrationRepository) => _registrationRepository = registrationRepository;

    public async Task<PagedResult<TeamRegistrationDto>> Handle(GetTeamFinancialsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _registrationRepository.GetQueryable().Where(r => r.TeamId == request.TeamId);
        var totalCount = await _registrationRepository.ExecuteCountAsync(query, ct);

        var projected = await _registrationRepository.ExecuteQueryAsync(query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new TeamRegistrationDto
            {
                Id = r.Id, TeamId = r.TeamId,
                TeamName = r.Team != null ? r.Team.Name : string.Empty,
                CaptainName = r.Team != null ? r.Team.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty : string.Empty,
                Status = r.Status.ToString(), PaymentReceiptUrl = r.PaymentReceiptUrl,
                SenderNumber = r.SenderNumber, RejectionReason = r.RejectionReason,
                PaymentMethod = r.PaymentMethod, RegisteredAt = r.CreatedAt, TournamentId = r.TournamentId
            }), ct);

        return new PagedResult<TeamRegistrationDto>(projected, totalCount, request.Page, pageSize);
    }
}

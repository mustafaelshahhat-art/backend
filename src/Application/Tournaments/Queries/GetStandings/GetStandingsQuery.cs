using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Tournaments.Queries.GetStandings;

public record GetStandingsQuery(
    Guid TournamentId,
    int Page = 1,
    int PageSize = 100,
    int? GroupId = null
) : IRequest<Application.Common.Models.PagedResult<TournamentStandingDto>>;

using Application.Common.Models;
using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamMatches;

public record GetTeamMatchesQuery(Guid TeamId, int Page, int PageSize) : IRequest<PagedResult<MatchDto>>;

using Application.Common.Models;
using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchesByTournament;

public record GetMatchesByTournamentQuery(Guid TournamentId, int Page = 1, int PageSize = 100) : IRequest<PagedResult<MatchDto>>;

using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchById;

public record GetMatchByIdQuery(Guid Id) : IRequest<MatchDto?>;

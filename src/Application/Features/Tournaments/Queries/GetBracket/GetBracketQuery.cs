using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetBracket;

public record GetBracketQuery(Guid TournamentId) : IRequest<BracketDto>;

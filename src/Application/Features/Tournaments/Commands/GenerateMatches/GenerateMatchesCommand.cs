using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Tournaments.Commands.GenerateMatches;

public record GenerateMatchesCommand(Guid TournamentId, Guid UserId, string UserRole) : IRequest<IEnumerable<MatchDto>>;

using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Tournaments.Commands.GenerateManualGroupMatches;

public record GenerateManualGroupMatchesCommand(
    Guid TournamentId,
    Guid UserId,
    string UserRole) : IRequest<IEnumerable<MatchDto>>;

using MediatR;

namespace Application.Features.Tournaments.Commands.WithdrawTeam;

public record WithdrawTeamCommand(Guid TournamentId, Guid TeamId, Guid UserId) : IRequest<Unit>;

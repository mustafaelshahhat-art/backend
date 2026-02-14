using MediatR;

namespace Application.Features.Tournaments.Commands.DeleteTournament;

public record DeleteTournamentCommand(Guid Id, Guid UserId, string UserRole) : IRequest<bool>;

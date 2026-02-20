using MediatR;

namespace Application.Features.Teams.Commands.DeleteTeam;

public record DeleteTeamCommand(Guid Id, Guid UserId, string UserRole) : IRequest<Unit>;

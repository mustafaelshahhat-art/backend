using MediatR;

namespace Application.Features.Teams.Commands.RemovePlayer;

public record RemovePlayerCommand(Guid TeamId, Guid PlayerId, Guid UserId, string UserRole) : IRequest<Unit>;

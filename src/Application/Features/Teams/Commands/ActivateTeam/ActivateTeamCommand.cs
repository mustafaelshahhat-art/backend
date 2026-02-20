using MediatR;

namespace Application.Features.Teams.Commands.ActivateTeam;

public record ActivateTeamCommand(Guid TeamId) : IRequest<Unit>;

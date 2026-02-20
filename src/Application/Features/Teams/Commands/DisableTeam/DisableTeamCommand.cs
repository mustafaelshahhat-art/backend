using MediatR;

namespace Application.Features.Teams.Commands.DisableTeam;

public record DisableTeamCommand(Guid TeamId) : IRequest<Unit>;

using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.RequestJoinTeam;

public record RequestJoinTeamCommand(Guid TeamId, Guid PlayerId) : IRequest<JoinRequestDto>;

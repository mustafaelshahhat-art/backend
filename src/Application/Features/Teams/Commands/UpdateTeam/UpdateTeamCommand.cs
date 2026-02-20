using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.UpdateTeam;

public record UpdateTeamCommand(Guid Id, UpdateTeamRequest Request, Guid UserId, string UserRole) : IRequest<TeamDto>;

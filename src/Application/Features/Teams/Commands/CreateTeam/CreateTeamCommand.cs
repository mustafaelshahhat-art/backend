using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.CreateTeam;

public record CreateTeamCommand(CreateTeamRequest Request, Guid CaptainId) : IRequest<TeamDto>;

using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamById;

public record GetTeamByIdQuery(Guid Id) : IRequest<TeamDto?>;

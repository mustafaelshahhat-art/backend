using Application.Common.Models;
using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamPlayers;

public record GetTeamPlayersQuery(Guid TeamId, int Page, int PageSize) : IRequest<PagedResult<PlayerDto>>;

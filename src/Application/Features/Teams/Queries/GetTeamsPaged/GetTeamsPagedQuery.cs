using Application.Common.Models;
using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamsPaged;

public record GetTeamsPagedQuery(int Page, int PageSize, Guid? CaptainId = null, Guid? PlayerId = null) : IRequest<PagedResult<TeamDto>>;

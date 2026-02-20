using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetGroups;

public record GetGroupsQuery(Guid TournamentId, int Page, int PageSize) : IRequest<PagedResult<GroupDto>>;

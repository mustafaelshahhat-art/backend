using Application.Common.Models;
using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Queries.GetJoinRequests;

public record GetJoinRequestsQuery(Guid TeamId, int Page, int PageSize) : IRequest<PagedResult<JoinRequestDto>>;

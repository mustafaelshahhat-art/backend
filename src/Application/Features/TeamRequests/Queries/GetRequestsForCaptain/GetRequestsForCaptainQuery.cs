using Application.Common.Models;
using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.TeamRequests.Queries.GetRequestsForCaptain;

public record GetRequestsForCaptainQuery(Guid UserId, int Page, int PageSize) : IRequest<PagedResult<JoinRequestDto>>;

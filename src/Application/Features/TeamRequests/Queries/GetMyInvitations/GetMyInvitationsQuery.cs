using Application.Common.Models;
using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.TeamRequests.Queries.GetMyInvitations;

public record GetMyInvitationsQuery(Guid UserId, int Page, int PageSize) : IRequest<PagedResult<JoinRequestDto>>;

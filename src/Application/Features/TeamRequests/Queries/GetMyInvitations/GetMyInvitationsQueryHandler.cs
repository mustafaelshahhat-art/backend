using Application.Common.Models;
using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.TeamRequests.Queries.GetMyInvitations;

public class GetMyInvitationsQueryHandler : IRequestHandler<GetMyInvitationsQuery, PagedResult<JoinRequestDto>>
{
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    public GetMyInvitationsQueryHandler(IRepository<TeamJoinRequest> joinRequestRepository) => _joinRequestRepository = joinRequestRepository;

    public async Task<PagedResult<JoinRequestDto>> Handle(GetMyInvitationsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            request.Page, pageSize,
            r => r.UserId == request.UserId && r.Status == "pending",
            q => q.OrderByDescending(r => r.CreatedAt), ct,
            r => r.Team!);

        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id, TeamId = r.TeamId,
            TeamName = r.Team?.Name ?? "Unknown",
            PlayerId = request.UserId,
            Status = r.Status, RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new PagedResult<JoinRequestDto>(dtos, totalCount, request.Page, pageSize);
    }
}

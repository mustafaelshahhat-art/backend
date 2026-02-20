using Application.Common.Models;
using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetJoinRequests;

public class GetJoinRequestsQueryHandler : IRequestHandler<GetJoinRequestsQuery, PagedResult<JoinRequestDto>>
{
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;
    public GetJoinRequestsQueryHandler(IRepository<TeamJoinRequest> joinRequestRepository) => _joinRequestRepository = joinRequestRepository;

    public async Task<PagedResult<JoinRequestDto>> Handle(GetJoinRequestsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            request.Page, pageSize,
            r => r.TeamId == request.TeamId,
            q => q.OrderByDescending(r => r.CreatedAt), ct,
            r => r.User!);

        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id, PlayerId = r.UserId,
            PlayerName = r.User?.Name ?? "Unknown",
            Status = r.Status, RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new PagedResult<JoinRequestDto>(dtos, totalCount, request.Page, pageSize);
    }
}

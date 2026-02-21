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

        // PERF-FIX: Server-side projection — previously loaded full User entity (including
        // PasswordHash, RefreshToken, NationalId, IdUrls) just to get User.Name.
        // Now uses Select() to fetch only the columns needed for the DTO.
        var query = _joinRequestRepository.GetQueryable()
            .Where(r => r.TeamId == request.TeamId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await _joinRequestRepository.ExecuteCountAsync(query, ct);

        var projectedQuery = query
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new JoinRequestDto
            {
                Id = r.Id,
                PlayerId = r.UserId,
                PlayerName = r.User != null ? r.User.Name : "Unknown",
                Status = r.Status,
                RequestDate = r.CreatedAt,
                InitiatedByPlayer = r.InitiatedByPlayer
            });

        var dtos = await _joinRequestRepository.ExecuteQueryAsync(projectedQuery, ct);

        return new PagedResult<JoinRequestDto>(dtos, totalCount, request.Page, pageSize);
    }
}

using Application.Common.Models;
using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.TeamRequests.Queries.GetRequestsForCaptain;

public class GetRequestsForCaptainQueryHandler : IRequestHandler<GetRequestsForCaptainQuery, PagedResult<JoinRequestDto>>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;

    public GetRequestsForCaptainQueryHandler(IRepository<Team> teamRepository, IRepository<TeamJoinRequest> joinRequestRepository)
    {
        _teamRepository = teamRepository;
        _joinRequestRepository = joinRequestRepository;
    }

    public async Task<PagedResult<JoinRequestDto>> Handle(GetRequestsForCaptainQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var teams = await _teamRepository.FindAsync(
            t => t.Players.Any(p => p.TeamRole == TeamRole.Captain && p.UserId == request.UserId),
            new[] { "Players" }, ct);
        var teamIds = teams.Select(t => t.Id).ToList();

        var (items, totalCount) = await _joinRequestRepository.GetPagedAsync(
            request.Page, pageSize,
            r => teamIds.Contains(r.TeamId) && r.Status == "pending",
            q => q.OrderByDescending(r => r.CreatedAt), ct,
            r => r.User!);

        var dtos = items.Select(r => new JoinRequestDto
        {
            Id = r.Id, TeamId = r.TeamId,
            TeamName = teams.FirstOrDefault(t => t.Id == r.TeamId)?.Name ?? "Unknown",
            PlayerId = r.UserId,
            PlayerName = r.User?.Name ?? "Unknown",
            Status = r.Status, RequestDate = r.CreatedAt,
            InitiatedByPlayer = r.InitiatedByPlayer
        }).ToList();

        return new PagedResult<JoinRequestDto>(dtos, totalCount, request.Page, pageSize);
    }
}

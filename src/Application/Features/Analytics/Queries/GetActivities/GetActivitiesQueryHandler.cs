using Application.Common;
using Application.Common.Models;
using Application.DTOs.Analytics;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Analytics.Queries.GetActivities;

public class GetActivitiesQueryHandler : IRequestHandler<GetActivitiesQuery, PagedResult<ActivityDto>>
{
    private readonly IRepository<Activity> _activityRepository;

    public GetActivitiesQueryHandler(IRepository<Activity> activityRepository)
    {
        _activityRepository = activityRepository;
    }

    public async Task<PagedResult<ActivityDto>> Handle(GetActivitiesQuery request, CancellationToken ct)
    {
        var isAdmin = request.CurrentUserRole == UserRole.Admin.ToString();
        var isCreator = request.CurrentUserRole == UserRole.TournamentCreator.ToString();

        if (!isAdmin && !isCreator)
            throw new Shared.Exceptions.ForbiddenException("Not authorized to view activities");

        Guid? creatorId = isCreator ? request.CurrentUserId : null;

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize > 100 ? 100 : (request.PageSize < 1 ? 20 : request.PageSize);

        var query = _activityRepository.GetQueryable();

        if (creatorId.HasValue)
            query = query.Where(a => a.UserId == creatorId.Value);

        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);

        if (!string.IsNullOrEmpty(request.ActorRole))
            query = query.Where(a => a.ActorRole == request.ActorRole);

        if (!string.IsNullOrEmpty(request.ActionType))
            query = query.Where(a => a.Type == request.ActionType);

        if (!string.IsNullOrEmpty(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);

        if (request.MinSeverity.HasValue)
            query = query.Where(a => (int)a.Severity >= request.MinSeverity.Value);

        if (request.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= request.ToDate.Value);

        var totalCount = await _activityRepository.ExecuteCountAsync(query, ct);

        var dtos = await _activityRepository.ExecuteQueryAsync(query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                ActionType = a.Type,
                Message = a.Message,
                Timestamp = a.CreatedAt,
                Time = "",
                UserName = a.UserName,
                Severity = a.Severity == Domain.Enums.ActivitySeverity.Critical ? "critical"
                         : a.Severity == Domain.Enums.ActivitySeverity.Warning ? "warning" : "info",
                ActorRole = a.ActorRole,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                EntityName = a.EntityName
            }), ct);

        foreach (var dto in dtos)
        {
            dto.Type = ActivityConstants.Library.TryGetValue(dto.ActionType, out var meta)
                ? meta.CategoryAr
                : "نظام";
        }

        return new PagedResult<ActivityDto>(dtos, totalCount, page, pageSize);
    }
}

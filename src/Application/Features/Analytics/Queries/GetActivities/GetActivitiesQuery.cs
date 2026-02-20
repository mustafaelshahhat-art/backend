using Application.DTOs.Analytics;
using Application.Common.Models;
using MediatR;

namespace Application.Features.Analytics.Queries.GetActivities;

public record GetActivitiesQuery(
    int Page,
    int PageSize,
    string? ActorRole,
    string? ActionType,
    string? EntityType,
    DateTime? FromDate,
    DateTime? ToDate,
    int? MinSeverity,
    Guid? UserId,
    Guid CurrentUserId,
    string CurrentUserRole) : IRequest<PagedResult<ActivityDto>>;

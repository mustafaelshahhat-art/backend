using Application.DTOs.Analytics;
using MediatR;

namespace Application.Features.Analytics.Queries.GetAnalyticsOverview;

public record GetAnalyticsOverviewQuery(Guid? TeamId, Guid UserId, string UserRole) : IRequest<object>;

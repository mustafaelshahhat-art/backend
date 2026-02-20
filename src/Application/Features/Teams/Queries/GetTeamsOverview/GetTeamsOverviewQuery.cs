using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamsOverview;

public record GetTeamsOverviewQuery(Guid UserId) : IRequest<TeamsOverviewDto>;

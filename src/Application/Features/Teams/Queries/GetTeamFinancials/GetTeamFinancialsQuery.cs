using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamFinancials;

public record GetTeamFinancialsQuery(Guid TeamId, int Page, int PageSize) : IRequest<PagedResult<TeamRegistrationDto>>;

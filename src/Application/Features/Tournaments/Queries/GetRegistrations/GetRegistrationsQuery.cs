using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetRegistrations;

public record GetRegistrationsQuery(Guid TournamentId, int Page, int PageSize) : IRequest<PagedResult<TeamRegistrationDto>>;

using Application.Common.Models;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetTournamentsPaged;

public record GetTournamentsPagedQuery(int Page, int PageSize, Guid? UserId = null, string? UserRole = null) : IRequest<PagedResult<TournamentDto>>;

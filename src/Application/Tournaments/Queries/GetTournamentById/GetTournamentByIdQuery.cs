using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Tournaments.Queries.GetTournamentById;

/// <summary>
/// Query to get a single tournament by ID with privacy filtering.
/// 
/// Query to retrieve a single tournament by ID with privacy filtering and intervention calculation.
/// </summary>
public record GetTournamentByIdQuery(
    Guid TournamentId,
    Guid? CurrentUserId = null,
    string? CurrentUserRole = null
) : IRequest<TournamentDto?>;

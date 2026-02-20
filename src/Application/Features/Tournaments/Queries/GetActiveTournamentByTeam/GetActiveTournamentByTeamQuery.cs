using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetActiveTournamentByTeam;

public record GetActiveTournamentByTeamQuery(Guid TeamId) : IRequest<TournamentDto?>;

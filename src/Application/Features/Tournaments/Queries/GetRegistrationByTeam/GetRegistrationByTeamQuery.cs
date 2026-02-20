using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Queries.GetRegistrationByTeam;

public record GetRegistrationByTeamQuery(Guid TournamentId, Guid TeamId) : IRequest<TeamRegistrationDto?>;

using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public record RegisterTeamCommand(Guid TournamentId, Guid TeamId, Guid UserId) : IRequest<TeamRegistrationDto>;

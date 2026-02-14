using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.EmergencyStart;

public record EmergencyStartCommand(Guid Id, Guid UserId, string UserRole) : IRequest<TournamentDto>;

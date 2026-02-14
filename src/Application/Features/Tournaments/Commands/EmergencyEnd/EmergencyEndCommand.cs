using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.EmergencyEnd;

public record EmergencyEndCommand(Guid Id, Guid UserId, string UserRole) : IRequest<TournamentDto>;

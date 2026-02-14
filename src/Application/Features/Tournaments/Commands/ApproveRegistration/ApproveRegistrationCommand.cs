using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.ApproveRegistration;

public record ApproveRegistrationCommand(Guid TournamentId, Guid TeamId, Guid UserId, string UserRole) : IRequest<TeamRegistrationDto>;

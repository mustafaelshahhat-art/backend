using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.CloseRegistration;

public record CloseRegistrationCommand(Guid Id, Guid UserId, string UserRole) : IRequest<TournamentDto>;

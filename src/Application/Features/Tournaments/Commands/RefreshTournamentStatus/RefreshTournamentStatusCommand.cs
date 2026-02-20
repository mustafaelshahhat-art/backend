using MediatR;
using Application.DTOs.Tournaments;

namespace Application.Features.Tournaments.Commands.RefreshTournamentStatus;

public record RefreshTournamentStatusCommand(Guid TournamentId) : IRequest<TournamentLifecycleResult>;

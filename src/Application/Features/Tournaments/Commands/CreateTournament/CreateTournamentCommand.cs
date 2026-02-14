using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.CreateTournament;

public record CreateTournamentCommand(CreateTournamentRequest Request, Guid? CreatorUserId) : IRequest<TournamentDto>;

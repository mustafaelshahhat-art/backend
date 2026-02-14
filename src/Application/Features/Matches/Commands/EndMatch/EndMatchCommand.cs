using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Commands.EndMatch;

public record EndMatchCommand(Guid Id, Guid UserId, string UserRole) : IRequest<MatchDto>;

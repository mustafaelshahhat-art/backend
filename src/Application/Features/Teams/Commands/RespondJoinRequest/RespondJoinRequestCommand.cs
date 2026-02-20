using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.Teams.Commands.RespondJoinRequest;

public record RespondJoinRequestCommand(Guid TeamId, Guid RequestId, bool Approve, Guid UserId, string UserRole) : IRequest<JoinRequestDto>;

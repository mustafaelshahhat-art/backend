using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.TeamRequests.Commands.AcceptInvite;

public record AcceptInviteCommand(Guid RequestId, Guid UserId) : IRequest<JoinRequestDto>;

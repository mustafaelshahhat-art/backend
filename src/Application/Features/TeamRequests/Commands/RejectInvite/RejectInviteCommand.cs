using Application.DTOs.Teams;
using MediatR;

namespace Application.Features.TeamRequests.Commands.RejectInvite;

public record RejectInviteCommand(Guid RequestId, Guid UserId) : IRequest<JoinRequestDto>;

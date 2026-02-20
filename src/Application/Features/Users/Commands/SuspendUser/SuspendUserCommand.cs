using MediatR;

namespace Application.Features.Users.Commands.SuspendUser;

public record SuspendUserCommand(Guid Id) : IRequest<Unit>;

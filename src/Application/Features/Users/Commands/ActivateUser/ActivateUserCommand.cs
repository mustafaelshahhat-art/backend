using MediatR;

namespace Application.Features.Users.Commands.ActivateUser;

public record ActivateUserCommand(Guid Id) : IRequest<Unit>;

using MediatR;

namespace Application.Features.Auth.Commands.Logout;

public record LogoutCommand(Guid UserId) : IRequest<Unit>;

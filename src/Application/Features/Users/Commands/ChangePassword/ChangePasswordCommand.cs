using MediatR;

namespace Application.Features.Users.Commands.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>;

using MediatR;

namespace Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(string Email, string Otp, string NewPassword) : IRequest<Unit>;

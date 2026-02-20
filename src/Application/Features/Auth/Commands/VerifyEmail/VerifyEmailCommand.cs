using MediatR;

namespace Application.Features.Auth.Commands.VerifyEmail;

public record VerifyEmailCommand(string Email, string Otp) : IRequest<Unit>;

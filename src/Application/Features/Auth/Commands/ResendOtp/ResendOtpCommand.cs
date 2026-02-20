using MediatR;

namespace Application.Features.Auth.Commands.ResendOtp;

public record ResendOtpCommand(string Email, string Type) : IRequest<Unit>;

using Application.DTOs.Auth;
using MediatR;

namespace Application.Features.Auth.Commands.Login;

public record LoginCommand(LoginRequest Request) : IRequest<AuthResponse>;

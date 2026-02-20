using Application.DTOs.Auth;
using MediatR;

namespace Application.Features.Auth.Commands.Register;

public record RegisterCommand(RegisterRequest Request) : IRequest<AuthResponse>;

using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Commands.UpdateUser;

public record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IRequest<UserDto>;

using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Commands.CreateAdmin;

public record CreateAdminCommand(CreateAdminRequest Request, Guid CreatedByAdminId) : IRequest<UserDto>;

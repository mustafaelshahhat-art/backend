using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDto?>;

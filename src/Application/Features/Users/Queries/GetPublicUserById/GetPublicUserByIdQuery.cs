using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Queries.GetPublicUserById;

public record GetPublicUserByIdQuery(Guid Id) : IRequest<UserPublicDto?>;

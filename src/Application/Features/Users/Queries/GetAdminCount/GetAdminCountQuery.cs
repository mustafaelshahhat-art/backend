using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Queries.GetAdminCount;

public record GetAdminCountQuery(Guid? UserId = null) : IRequest<AdminCountDto>;

using Application.Common.Models;
using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Queries.GetPublicUsersPaged;

public record GetPublicUsersPagedQuery(int Page, int PageSize, string? Role = null) : IRequest<PagedResult<UserPublicDto>>;

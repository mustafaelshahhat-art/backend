using Application.Common.Models;
using Application.DTOs.Users;
using MediatR;

namespace Application.Features.Users.Queries.GetUsersPaged;

public record GetUsersPagedQuery(int Page, int PageSize, string? Role = null) : IRequest<PagedResult<UserDto>>;

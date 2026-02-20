using Application.Common.Models;
using Application.DTOs.Users;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetUsersPaged;

public class GetUsersPagedQueryHandler : IRequestHandler<GetUsersPagedQuery, PagedResult<UserDto>>
{
    private readonly IRepository<User> _userRepository;

    public GetUsersPagedQueryHandler(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResult<UserDto>> Handle(GetUsersPagedQuery request, CancellationToken ct)
    {
        var pageNumber = request.Page;
        var pageSize = request.PageSize;
        var role = request.Role;

        if (pageSize > 100) pageSize = 100;

        var query = _userRepository.GetQueryable()
            .Where(u => u.IsEmailVerified);

        if (!string.IsNullOrEmpty(role) && Enum.TryParse<Domain.Enums.UserRole>(role, true, out var userRole))
            query = query.Where(u => u.Role == userRole);

        var totalCount = await _userRepository.ExecuteCountAsync(query, ct);

        var dtos = await _userRepository.ExecuteQueryAsync(query
            .OrderBy(u => u.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                DisplayId = u.DisplayId,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                Phone = u.Phone,
                Age = u.Age,
                GovernorateId = u.GovernorateId,
                GovernorateNameAr = u.GovernorateNav != null ? u.GovernorateNav.NameAr : null,
                CityId = u.CityId,
                CityNameAr = u.CityNav != null ? u.CityNav.NameAr : null,
                AreaId = u.AreaId,
                AreaNameAr = u.AreaNav != null ? u.AreaNav.NameAr : null,
                TeamId = u.TeamId,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            }), ct);

        return new PagedResult<UserDto>(dtos, totalCount, pageNumber, pageSize);
    }
}

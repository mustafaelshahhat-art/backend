using System.Linq.Expressions;
using Application.Common.Models;
using Application.DTOs.Users;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetPublicUsersPaged;

public class GetPublicUsersPagedQueryHandler : IRequestHandler<GetPublicUsersPagedQuery, PagedResult<UserPublicDto>>
{
    private readonly IRepository<User> _userRepository;
    private readonly IMapper _mapper;

    public GetPublicUsersPagedQueryHandler(IRepository<User> userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserPublicDto>> Handle(GetPublicUsersPagedQuery request, CancellationToken ct)
    {
        var pageNumber = request.Page;
        var pageSize = request.PageSize;
        var role = request.Role;

        if (pageSize > 100) pageSize = 100;

        Expression<Func<User, bool>>? predicate = null;

        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            if (userRole == UserRole.Admin)
                return new PagedResult<UserPublicDto>(new List<UserPublicDto>(), 0, pageNumber, pageSize);

            predicate = u => u.Role == userRole && u.IsEmailVerified;
        }
        else
        {
            predicate = u => u.Role != UserRole.Admin && u.IsEmailVerified;
        }

        var result = await _userRepository.GetPagedAsync(
            pageNumber, pageSize, predicate, q => q.OrderBy(u => u.Name), ct);

        var dtos = _mapper.Map<List<UserPublicDto>>(result.Items);

        return new PagedResult<UserPublicDto>(dtos, result.TotalCount, pageNumber, pageSize);
    }
}

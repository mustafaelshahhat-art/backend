using Application.DTOs.Users;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetAdminCount;

public class GetAdminCountQueryHandler : IRequestHandler<GetAdminCountQuery, AdminCountDto>
{
    private readonly IRepository<User> _userRepository;

    public GetAdminCountQueryHandler(IRepository<User> userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminCountDto> Handle(GetAdminCountQuery request, CancellationToken ct)
    {
        var userId = request.UserId;

        var admins = await _userRepository.FindAsync(
            u => u.Role == UserRole.Admin && u.Status != UserStatus.Suspended && u.IsEmailVerified, ct);

        var adminList = admins.ToList();
        var totalAdmins = adminList.Count;
        var isLastAdmin = false;

        if (userId.HasValue && totalAdmins == 1)
        {
            isLastAdmin = adminList.Any(a => a.Id == userId.Value);
        }

        return new AdminCountDto
        {
            TotalAdmins = totalAdmins,
            IsLastAdmin = isLastAdmin
        };
    }
}

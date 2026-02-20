using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly IUserCacheService _userCache;

    public DeleteUserCommandHandler(
        IRepository<User> userRepository,
        IRealTimeNotifier realTimeNotifier,
        IUserCacheService userCache)
    {
        _userRepository = userRepository;
        _realTimeNotifier = realTimeNotifier;
        _userCache = userCache;
    }

    public async Task<Unit> Handle(DeleteUserCommand command, CancellationToken ct)
    {
        var id = command.Id;

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        if (user.Role == UserRole.Admin)
        {
            var admins = await _userRepository.FindAsync(
                u => u.Role == UserRole.Admin && u.Status != UserStatus.Suspended && u.IsEmailVerified, ct);
            var adminList = admins.ToList();

            if (adminList.Count == 1 && adminList.Any(a => a.Id == id))
                throw new BadRequestException("لا يمكن حذف آخر مشرف في النظام");
        }

        await _realTimeNotifier.SendAccountStatusChangedAsync(id, "Deleted");
        await _realTimeNotifier.SendUserDeletedAsync(id);
        await _userRepository.DeleteAsync(user, ct);
        await _userCache.InvalidateUserAsync(id, ct);

        return Unit.Value;
    }
}

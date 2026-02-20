using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.SuspendUser;

public class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IUserCacheService _userCache;

    public SuspendUserCommandHandler(
        IRepository<User> userRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier,
        INotificationService notificationService,
        IUserCacheService userCache)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _notificationService = notificationService;
        _userCache = userCache;
    }

    public async Task<Unit> Handle(SuspendUserCommand command, CancellationToken ct)
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
                throw new BadRequestException("لا يمكن إيقاف آخر مدير نشط في النظام.");
        }

        user.Status = UserStatus.Suspended;
        user.TokenVersion++;
        await _userRepository.UpdateAsync(user, ct);

        await _userCache.InvalidateUserAsync(id, ct);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Suspended.ToString());
        await _notificationService.SendNotificationByTemplateAsync(
            id, NotificationTemplates.ACCOUNT_SUSPENDED, entityId: id, entityType: "user", ct: ct);

        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);
        await _realTimeNotifier.SendSystemEventAsync("USER_BLOCKED", new { UserId = id }, $"user:{id}", ct);

        return Unit.Value;
    }
}

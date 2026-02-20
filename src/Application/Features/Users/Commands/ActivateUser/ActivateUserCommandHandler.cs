using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.ActivateUser;

public class ActivateUserCommandHandler : IRequestHandler<ActivateUserCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly INotificationService _notificationService;

    public ActivateUserCommandHandler(
        IRepository<User> userRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<Unit> Handle(ActivateUserCommand command, CancellationToken ct)
    {
        var id = command.Id;

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user, ct);

        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Active.ToString());
        await _notificationService.SendNotificationByTemplateAsync(
            id, NotificationTemplates.ACCOUNT_APPROVED, entityId: id, entityType: "user", ct: ct);

        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);
        await _realTimeNotifier.SendSystemEventAsync("USER_APPROVED", new { UserId = id }, $"user:{id}", ct);

        return Unit.Value;
    }
}

using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly INotificationService _notificationService;

    public ChangePasswordCommandHandler(
        IRepository<User> userRepository,
        IPasswordHasher passwordHasher,
        IRealTimeNotifier realTimeNotifier,
        INotificationService notificationService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _realTimeNotifier = realTimeNotifier;
        _notificationService = notificationService;
    }

    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var userId = command.UserId;
        var currentPassword = command.CurrentPassword;
        var newPassword = command.NewPassword;

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null) throw new NotFoundException(nameof(User), userId);

        if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
            throw new BadRequestException("كلمة المرور الحالية غير صحيحة");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new BadRequestException("كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل");

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        user.TokenVersion++;
        await _userRepository.UpdateAsync(user, ct);

        await _realTimeNotifier.SendAccountStatusChangedAsync(userId, "PasswordChanged");
        await _notificationService.SendNotificationByTemplateAsync(
            userId, NotificationTemplates.PASSWORD_CHANGED, entityId: userId, entityType: "user", ct: ct);

        return Unit.Value;
    }
}

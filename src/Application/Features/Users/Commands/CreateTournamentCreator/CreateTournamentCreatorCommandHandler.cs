using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Users.Commands.CreateTournamentCreator;

public class CreateTournamentCreatorCommandHandler : IRequestHandler<CreateTournamentCreatorCommand, UserDto>
{
    private readonly IRepository<User> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ISystemSettingsService _systemSettingsService;

    public CreateTournamentCreatorCommandHandler(
        IRepository<User> userRepository,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier,
        ISystemSettingsService systemSettingsService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<UserDto> Handle(CreateTournamentCreatorCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (await _systemSettingsService.IsMaintenanceModeEnabledAsync(ct))
            throw new BadRequestException("لا يمكن إنشاء مستخدمين جدد أثناء وضع الصيانة");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new BadRequestException("البريد الإلكتروني مطلوب");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("الاسم مطلوب");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            throw new BadRequestException("كلمة المرور مطلوبة ويجب أن تكون 6 أحرف على الأقل");

        var email = request.Email.Trim().ToLower();

        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email, true, ct);
        if (existingUser != null && existingUser.Any())
            throw new ConflictException("البريد الإلكتروني مستخدم بالفعل");

        var newCreator = new User
        {
            Email = email,
            Name = request.Name.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.TournamentCreator,
            Status = request.Status,
            IsEmailVerified = true,
            DisplayId = "CRT-" + new Random().Next(1000, 9999)
        };

        await _userRepository.AddAsync(newCreator, ct);

        var dto = _mapper.Map<UserDto>(newCreator);
        await _realTimeNotifier.SendUserCreatedAsync(dto, ct);

        return dto;
    }
}

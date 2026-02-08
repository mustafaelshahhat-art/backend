using System;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Shared.Exceptions;
using Domain.Enums;
using AutoMapper;
using System.Linq;
using Application.DTOs.Users;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly IRealTimeNotifier _notifier;

    public AuthService(
        IRepository<User> userRepository, 
        IRepository<Team> teamRepository, 
        IJwtTokenGenerator jwtTokenGenerator, 
        IPasswordHasher passwordHasher, 
        IMapper mapper, 
        IAnalyticsService analyticsService,
        IRealTimeNotifier notifier)
    {
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notifier = notifier;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email and Name are required.");
        }

        var email = request.Email.Trim().ToLower();
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
        if (existingUser != null && existingUser.Any())
        {
            throw new ConflictException("Email already exists.");
        }

        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = (request.Role == UserRole.Admin) ? UserRole.Player : request.Role,
            Status = UserStatus.Pending, 
            DisplayId = "U-" + new Random().Next(1000, 9999),
            Phone = request.Phone?.Trim(),
            NationalId = request.NationalId?.Trim(),
            Age = request.Age,
            Governorate = request.Governorate,
            City = request.City,
            Neighborhood = request.Neighborhood,
            IdFrontUrl = request.IdFrontUrl,
            IdBackUrl = request.IdBackUrl
        };

        if (user.Role == UserRole.Player && string.IsNullOrEmpty(user.DisplayId))
        {
             // Logic for specific display ID can be better
        }

        await _userRepository.AddAsync(user);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);
        await _analyticsService.LogActivityAsync("User Registered", $"User {user.Name} registered.", user.Id, user.Name);

        var mappedUser = await MapUserWithTeamInfoAsync(user);

        // Real-time Event - Notify Admins/Users list
        await _notifier.SendUserCreatedAsync(mappedUser);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            User = mappedUser
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email is required.");
        }

        var email = request.Email.Trim().ToLower();
        var users = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
        var user = users.FirstOrDefault();

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new BadRequestException("Invalid email or password.");
        }

        if (user.Status == UserStatus.Suspended)
        {
             throw new ForbiddenException("User is suspended.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);
        
        try 
        {
            await _analyticsService.LogActivityAsync("User Login", $"User {user.Name} logged in.", user.Id, user.Name);
        }
        catch 
        {
            // Don't fail login if analytics logging fails
        }

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            User = await MapUserWithTeamInfoAsync(user)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var users = await _userRepository.FindAsync(u => u.RefreshToken == request.RefreshToken);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new BadRequestException("Invalid or expired refresh token.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = newRefreshToken,
            User = await MapUserWithTeamInfoAsync(user)
        };
    }
    private async Task<UserDto> MapUserWithTeamInfoAsync(User user)
    {
        var dto = _mapper.Map<UserDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value);
            if (team != null)
            {
                dto.TeamName = team.Name;
                dto.IsTeamOwner = team.CaptainId == user.Id;
            }
        }
        return dto;
    }
}

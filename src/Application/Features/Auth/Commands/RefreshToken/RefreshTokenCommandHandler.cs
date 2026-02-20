using Application.DTOs.Auth;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IMapper _mapper;

    public RefreshTokenCommandHandler(
        IRepository<User> userRepository, IRepository<Team> teamRepository,
        IRepository<Player> playerRepository, IJwtTokenGenerator jwtTokenGenerator, IMapper mapper)
    {
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _mapper = mapper;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var users = await _userRepository.FindAsync(u => u.RefreshToken == request.Request.RefreshToken, ct);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new BadRequestException("انتهت صلاحية الجلسة. يرجى تسجيل الدخول مرة أخرى.");

        var token = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);

        return new AuthResponse
        {
            Token = token, RefreshToken = newRefreshToken,
            User = await AuthUserMapper.MapUserWithTeamInfoAsync(user, _mapper, _teamRepository, _playerRepository, ct)
        };
    }
}

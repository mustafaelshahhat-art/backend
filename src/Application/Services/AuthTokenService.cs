using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Combines JWT token generation and password hashing into a single service.
/// Reduces 2 deps (IJwtTokenGenerator, IPasswordHasher) to 1 in auth handlers.
/// </summary>
public class AuthTokenService : IAuthTokenService
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public AuthTokenService(IJwtTokenGenerator jwtTokenGenerator, IPasswordHasher passwordHasher)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
    }

    public string GenerateToken(User user) => _jwtTokenGenerator.GenerateToken(user);
    public string GenerateRefreshToken() => _jwtTokenGenerator.GenerateRefreshToken();
    public string HashPassword(string password) => _passwordHasher.HashPassword(password);
    public bool VerifyPassword(string password, string hash) => _passwordHasher.VerifyPassword(password, hash);
}

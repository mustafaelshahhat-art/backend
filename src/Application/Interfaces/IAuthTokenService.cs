using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Combines JWT token generation and password hashing operations.
/// Reduces constructor dependencies in auth handlers.
/// </summary>
public interface IAuthTokenService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

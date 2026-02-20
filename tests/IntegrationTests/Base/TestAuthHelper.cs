using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Base;

public static class TestAuthHelper
{
    private const string TestSecret = "SuperSecretTestKeyForIntegrationTestsThatIsLongEnough123!";

    public static string GenerateToken(Guid userId, string email, string role, string name = "Test User", int tokenVersion = 1)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim("name", name),
            new Claim("status", "Active"),
            new Claim("token_version", tokenVersion.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: "KoraZone365Api",
            audience: "KoraZone365App",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid userId, string email, string role)
    {
        var client = factory.CreateClient();
        var token = GenerateToken(userId, email, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Seeds a User record in the database so the OnTokenValidated handler 
    /// can verify token_version against the DB. Returns the seeded User's Id.
    /// </summary>
    public static async Task<User> SeedUserAsync(
        WebApplicationFactory<Program> factory,
        Guid? userId = null,
        string name = "Test User",
        string email = "testuser@test.com",
        UserRole role = UserRole.Player,
        UserStatus status = UserStatus.Active,
        int tokenVersion = 1)
    {
        var id = userId ?? Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IPasswordHasher>();

        var user = new User
        {
            Id = id,
            Name = name,
            Email = email,
            PasswordHash = passwordHasher.HashPassword("Test1234!"),
            Role = role,
            Status = status,
            IsEmailVerified = true,
            DisplayId = $"TST-{id.ToString()[..4]}",
            TokenVersion = tokenVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a user and returns an HttpClient with a valid JWT for that user.
    /// </summary>
    public static async Task<(HttpClient Client, User User)> CreateSeededAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string name = "Test User",
        string email = "testuser@test.com",
        UserRole role = UserRole.Player)
    {
        var user = await SeedUserAsync(factory, name: name, email: email, role: role);
        var client = CreateAuthenticatedClient(factory, user.Id, user.Email, role.ToString());
        return (client, user);
    }
}

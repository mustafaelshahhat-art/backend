using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Api.Infrastructure;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Try Sub claim (JwtRegisteredClaimNames.Sub)
        var subClaim = connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            return subClaim;
        }

        // Fallback to NameIdentifier
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

using System.Security.Claims;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Api.Middleware;

public class UserStatusCheckMiddleware
{
    private readonly RequestDelegate _next;

    public UserStatusCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserAccessor userAccessor)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = userAccessor.User;

            // If user doesn't exist (deleted) or is suspended, return 403 Forbidden
            if (user == null || user.Status == UserStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Account is disabled or does not exist.");
                return;
            }
        }

        await _next(context);
    }
}

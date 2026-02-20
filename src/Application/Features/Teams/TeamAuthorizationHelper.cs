using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Teams;

internal static class TeamAuthorizationHelper
{
    public static async Task ValidateManagementRights(
        IRepository<Team> teamRepository, Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        if (userRole == UserRole.Admin.ToString()) return;

        var team = await teamRepository.GetByIdNoTrackingAsync(teamId,
            new Expression<Func<Team, object>>[] { t => t.Players }, ct);
        if (team == null) throw new NotFoundException(nameof(Team), teamId);

        var isCaptain = team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain);
        if (!isCaptain)
            throw new ForbiddenException("غير مصرح لك بإدارة هذا الفريق. فقط قائد الفريق أو مدير النظام يمكنه ذلك.");
    }
}

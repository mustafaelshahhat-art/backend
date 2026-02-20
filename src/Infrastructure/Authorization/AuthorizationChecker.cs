using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Infrastructure.Authorization;

/// <summary>
/// Imperative authorization checker for use in MediatR command handlers.
/// Replaces the inline ValidateOwnership/ValidateManagementRights patterns
/// found in TournamentService and TeamService.
///
/// Mirrors the exact logic from:
/// - TournamentService.ValidateOwnership() (CreatorUserId + TournamentCreator role check)
/// - TeamService.ValidateManagementRights() (Player table Captain role check)
/// </summary>
public class AuthorizationChecker : IAuthorizationChecker
{
    private readonly IRepository<Tournament> _tournamentRepo;
    private readonly IRepository<Player> _playerRepo;

    public AuthorizationChecker(
        IRepository<Tournament> tournamentRepo,
        IRepository<Player> playerRepo)
    {
        _tournamentRepo = tournamentRepo;
        _playerRepo = playerRepo;
    }

    /// <inheritdoc />
    public async Task EnsureTournamentOwnerAsync(
        Guid tournamentId, Guid userId, string userRole, CancellationToken ct = default)
    {
        // Admin bypasses all checks — matches TournamentService.ValidateOwnership
        if (userRole == UserRole.Admin.ToString())
            return;

        var tournament = await _tournamentRepo.GetByIdAsync(tournamentId, ct)
            ?? throw new NotFoundException(nameof(Tournament), tournamentId);

        var isOwner = userRole == UserRole.TournamentCreator.ToString()
                      && tournament.CreatorUserId == userId;

        if (!isOwner)
            throw new ForbiddenException(
                "غير مصرح لك بإدارة هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
    }

    /// <inheritdoc />
    public async Task EnsureTeamCaptainAsync(
        Guid teamId, Guid userId, string userRole, CancellationToken ct = default)
    {
        // Admin bypasses all checks — matches TeamService.ValidateManagementRights
        if (userRole == UserRole.Admin.ToString())
            return;

        // Query Player table for captain match — same pattern as TeamCaptainHandler
        var players = await _playerRepo.FindAsync(
            p => p.TeamId == teamId && p.UserId == userId && p.TeamRole == TeamRole.Captain, ct);

        if (!players.Any())
            throw new ForbiddenException(
                "غير مصرح لك بإدارة هذا الفريق. فقط قائد الفريق أو مدير النظام يمكنه ذلك.");
    }
}

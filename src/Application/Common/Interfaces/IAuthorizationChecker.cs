namespace Application.Common.Interfaces;

/// <summary>
/// Validates whether the current user has ownership/management rights
/// over a tournament or team. Extracts the repeated ValidateOwnership()
/// pattern found in TournamentService and TeamService.
/// 
/// Can be used in MediatR pipeline behavior or directly in handlers
/// during the transition period.
/// </summary>
public interface IAuthorizationChecker
{
    /// <summary>
    /// Throws ForbiddenException if user is not admin and not the tournament creator.
    /// </summary>
    Task EnsureTournamentOwnerAsync(Guid tournamentId, Guid userId, string userRole, CancellationToken ct = default);

    /// <summary>
    /// Throws ForbiddenException if user is not admin and not the team captain.
    /// </summary>
    Task EnsureTeamCaptainAsync(Guid teamId, Guid userId, string userRole, CancellationToken ct = default);
}

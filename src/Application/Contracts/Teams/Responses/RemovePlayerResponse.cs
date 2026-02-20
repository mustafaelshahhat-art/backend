namespace Application.Contracts.Teams.Responses;

/// <summary>
/// Response for RemovePlayer endpoint.
/// Replaces anonymous { teamRemoved, playerId, teamId }.
/// </summary>
public class RemovePlayerResponse
{
    public bool TeamRemoved { get; set; }
    public Guid PlayerId { get; set; }
    public Guid TeamId { get; set; }
}

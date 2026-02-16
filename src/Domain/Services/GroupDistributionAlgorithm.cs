namespace Domain.Services;

/// <summary>
/// Pure, deterministic group distribution algorithm.
/// Distributes N teams across G groups with at most 1 team difference between groups.
/// No empty groups when teams exist. Works for any N ≥ 1.
/// 
/// Unit-testable — zero dependencies on EF Core, repositories, or infrastructure.
/// </summary>
public static class GroupDistributionAlgorithm
{
    /// <summary>
    /// Distributes team IDs across groups evenly.
    /// 
    /// Algorithm:
    ///   BaseSize  = N / G
    ///   Remainder = N % G
    ///   First <paramref name="remainder"/> groups get BaseSize + 1 teams.
    ///   Remaining groups get BaseSize teams.
    /// 
    /// Examples:
    ///   N=10, G=4 → [3, 3, 2, 2]
    ///   N=16, G=4 → [4, 4, 4, 4]
    ///   N=18, G=4 → [5, 5, 4, 4]
    ///   N=7,  G=4 → [2, 2, 2, 1]
    ///   N=3,  G=4 → [1, 1, 1]  (only 3 groups created — no empty groups)
    /// </summary>
    /// <param name="teamIds">Ordered list of team IDs to distribute (pre-shuffled if random).</param>
    /// <param name="numberOfGroups">Desired number of groups (G).</param>
    /// <param name="openingTeamAId">Optional opening team A — will be placed together in group 1.</param>
    /// <param name="openingTeamBId">Optional opening team B — will be placed together in group 1.</param>
    /// <returns>
    /// Dictionary mapping 1-based groupId → list of team IDs.
    /// Group count is min(N, G) to prevent empty groups.
    /// </returns>
    /// <exception cref="ArgumentException">If inputs are invalid.</exception>
    public static Dictionary<int, List<Guid>> Distribute(
        IReadOnlyList<Guid> teamIds,
        int numberOfGroups,
        Guid? openingTeamAId = null,
        Guid? openingTeamBId = null)
    {
        if (teamIds == null || teamIds.Count == 0)
            throw new ArgumentException("Team list cannot be empty.", nameof(teamIds));

        if (numberOfGroups < 1)
            throw new ArgumentException("Number of groups must be at least 1.", nameof(numberOfGroups));

        // Validate no duplicates
        if (teamIds.Count != teamIds.Distinct().Count())
            throw new ArgumentException("Duplicate team IDs detected.", nameof(teamIds));

        // Validate opening teams exist in the list if provided
        bool hasOpening = openingTeamAId.HasValue && openingTeamBId.HasValue;
        if (hasOpening)
        {
            if (openingTeamAId == openingTeamBId)
                throw new ArgumentException("Opening teams must be different.");

            if (!teamIds.Contains(openingTeamAId!.Value))
                throw new ArgumentException("Opening team A is not in the team list.");

            if (!teamIds.Contains(openingTeamBId!.Value))
                throw new ArgumentException("Opening team B is not in the team list.");
        }

        int N = teamIds.Count;

        // CORE RULE: Never create empty groups. Reduce group count if N < G.
        int G = Math.Min(N, numberOfGroups);

        // Initialize groups (1-based IDs)
        var groups = new Dictionary<int, List<Guid>>();
        for (int i = 1; i <= G; i++)
            groups[i] = new List<Guid>();

        if (hasOpening)
        {
            return DistributeWithOpening(teamIds, G, groups, openingTeamAId!.Value, openingTeamBId!.Value);
        }

        return DistributeEven(teamIds, G, groups);
    }

    /// <summary>
    /// Even distribution without opening team constraints.
    /// Uses round-robin modulo assignment: team[i] → group (i % G) + 1.
    /// Guarantees max difference of 1 team between any two groups.
    /// </summary>
    private static Dictionary<int, List<Guid>> DistributeEven(
        IReadOnlyList<Guid> teamIds, int G, Dictionary<int, List<Guid>> groups)
    {
        for (int i = 0; i < teamIds.Count; i++)
        {
            int groupId = (i % G) + 1;
            groups[groupId].Add(teamIds[i]);
        }

        return groups;
    }

    /// <summary>
    /// Distribution with opening teams locked to group 1.
    /// Opening teams are placed first, then remaining teams fill
    /// the smallest group (greedy, stable) to maintain balance.
    /// </summary>
    private static Dictionary<int, List<Guid>> DistributeWithOpening(
        IReadOnlyList<Guid> teamIds, int G, Dictionary<int, List<Guid>> groups,
        Guid openingA, Guid openingB)
    {
        // Place opening teams in group 1
        groups[1].Add(openingA);
        groups[1].Add(openingB);

        // Remaining teams (order preserved from input — pre-shuffled if random mode)
        var remaining = teamIds.Where(id => id != openingA && id != openingB).ToList();

        // Fill smallest group first (greedy balance)
        foreach (var teamId in remaining)
        {
            int targetGroup = 1;
            int minCount = groups[1].Count;

            for (int g = 2; g <= G; g++)
            {
                if (groups[g].Count < minCount)
                {
                    minCount = groups[g].Count;
                    targetGroup = g;
                }
            }

            groups[targetGroup].Add(teamId);
        }

        return groups;
    }

    /// <summary>
    /// Validates that a distribution result satisfies all invariants:
    /// 1. No empty groups
    /// 2. All teams assigned exactly once
    /// 3. Max group size difference ≤ 1
    /// 4. Group count ≤ requested numberOfGroups
    /// </summary>
    public static ValidationResult Validate(
        Dictionary<int, List<Guid>> distribution,
        IReadOnlyList<Guid> originalTeamIds,
        int requestedGroups)
    {
        var errors = new List<string>();

        // Check for empty groups
        foreach (var (groupId, teams) in distribution)
        {
            if (teams.Count == 0)
                errors.Add($"Group {groupId} is empty.");
        }

        // Check all teams assigned exactly once
        var allAssigned = distribution.Values.SelectMany(t => t).ToList();
        if (allAssigned.Count != originalTeamIds.Count)
            errors.Add($"Expected {originalTeamIds.Count} teams, but {allAssigned.Count} were assigned.");

        var missing = originalTeamIds.Except(allAssigned).ToList();
        if (missing.Any())
            errors.Add($"{missing.Count} teams were not assigned to any group.");

        var duplicates = allAssigned.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Any())
            errors.Add($"{duplicates.Count} teams appear in multiple groups.");

        // Check max difference ≤ 1
        if (distribution.Values.Any())
        {
            int maxSize = distribution.Values.Max(g => g.Count);
            int minSize = distribution.Values.Min(g => g.Count);
            if (maxSize - minSize > 1)
                errors.Add($"Group size imbalance: max={maxSize}, min={minSize}, diff={maxSize - minSize}.");
        }

        // Check group count
        if (distribution.Count > requestedGroups)
            errors.Add($"Created {distribution.Count} groups but only {requestedGroups} were requested.");

        return new ValidationResult(errors.Count == 0, errors);
    }

    public record ValidationResult(bool IsValid, List<string> Errors);
}

using System;

namespace Domain.Entities;

public class TeamStats : BaseEntity
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int MatchesPlayed { get; set; }

    public void Reset()
    {
        Wins = 0;
        Losses = 0;
        Draws = 0;
        GoalsFor = 0;
        GoalsAgainst = 0;
        MatchesPlayed = 0;
    }

    public void UpdateFromMatch(int teamScore, int opponentScore)
    {
        MatchesPlayed++;
        GoalsFor += teamScore;
        GoalsAgainst += opponentScore;

        if (teamScore > opponentScore) Wins++;
        else if (teamScore == opponentScore) Draws++;
        else Losses++;
    }
}

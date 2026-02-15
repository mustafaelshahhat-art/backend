using System;
using Domain.Entities;
using Domain.Interfaces;

namespace Domain.Events;

public class MatchFinishedEvent : IDomainEvent
{
    public Match Match { get; }
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    public MatchFinishedEvent(Match match)
    {
        Match = match;
    }
}

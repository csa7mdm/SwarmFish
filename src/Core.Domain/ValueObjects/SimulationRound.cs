using SwarmFish.Core.Contracts.Models;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a snapshot of one simulation round.
/// </summary>
public sealed class SimulationRound
{
    /// <summary>Gets the round number.</summary>
    public int RoundNumber { get; }

    /// <summary>Gets all agent events produced during this round.</summary>
    public IReadOnlyList<AgentEvent> Events { get; }

    /// <summary>Gets the duration of this round.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Gets the total number of events in this round.</summary>
    public int EventCount { get; }

    /// <summary>Gets the number of unique agents that participated in this round.</summary>
    public int UniqueAgents { get; }

    /// <summary>Gets the most frequent event type in this round.</summary>
    public string DominantEventType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SimulationRound"/>.
    /// </summary>
    /// <param name="roundNumber">The round number. Must be at least 1.</param>
    /// <param name="events">The agent events from this round. Must not be null.</param>
    /// <param name="duration">The duration of this round. Must be non-negative.</param>
    /// <exception cref="DomainException">Thrown when invariants are violated.</exception>
    public SimulationRound(int roundNumber, IReadOnlyList<AgentEvent> events, TimeSpan duration)
    {
        if (roundNumber < 1)
            throw new DomainException("Round number must be at least 1.");
        ArgumentNullException.ThrowIfNull(events);
        if (duration < TimeSpan.Zero)
            throw new DomainException("Duration must be non-negative.");

        RoundNumber = roundNumber;
        Events = events;
        Duration = duration;

        // Compute aggregate stats
        EventCount = events.Count;
        UniqueAgents = events.Select(e => e.AgentId).Distinct().Count();
        DominantEventType = events.Count > 0
            ? events.GroupBy(e => e.EventType)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key
            : string.Empty;
    }
}

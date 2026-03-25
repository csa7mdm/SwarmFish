namespace SwarmFish.Core.Domain.Events;

/// <summary>
/// Abstract base record for all domain events in the SwarmFish simulation.
/// </summary>
/// <param name="EventId">The unique identifier for this event occurrence.</param>
/// <param name="OccurredAt">The timestamp when this event occurred.</param>
public abstract record DomainEvent(Guid EventId, DateTimeOffset OccurredAt)
{
    /// <summary>
    /// Creates a new domain event with a generated ID and the current timestamp.
    /// </summary>
    protected DomainEvent() : this(Guid.NewGuid(), DateTimeOffset.UtcNow)
    {
    }
}

namespace SwarmFish.Core.Domain.Events;

/// <summary>
/// Raised when a simulation round completes.
/// </summary>
/// <param name="SimulationId">The identifier of the simulation.</param>
/// <param name="Round">The round number that completed.</param>
/// <param name="EventCount">The total number of agent events in this round.</param>
/// <param name="UniqueAgentCount">The number of unique agents that participated in this round.</param>
public record RoundCompleted(Guid SimulationId, int Round, int EventCount, int UniqueAgentCount)
    : DomainEvent();

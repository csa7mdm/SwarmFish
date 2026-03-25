using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Core.Contracts.Models;

/// <summary>
/// Represents a single simulation tick dispatched to agents each round.
/// </summary>
/// <param name="Round">The current round number.</param>
/// <param name="SimulatedTime">The simulated point in time for this tick.</param>
/// <param name="PreviousEvents">Events produced by agents in the previous round.</param>
/// <param name="Context">Simulation-level context metadata.</param>
public record SimulationTick(
    int Round,
    DateTimeOffset SimulatedTime,
    IReadOnlyList<AgentEvent> PreviousEvents,
    SimulationContext Context
);

/// <summary>
/// Represents an event produced by an agent during a simulation tick.
/// </summary>
/// <param name="AgentId">The unique identifier of the agent that produced this event.</param>
/// <param name="EventType">The type of event (e.g., "spoke", "moved", "reacted", "silent").</param>
/// <param name="Payload">JSON blob containing event-specific data.</param>
/// <param name="Timestamp">The timestamp when this event was produced.</param>
public record AgentEvent(
    Guid AgentId,
    string EventType,
    string Payload,
    DateTimeOffset Timestamp
);

/// <summary>
/// Provides simulation-level context metadata for a tick.
/// </summary>
/// <param name="SimulationId">The unique identifier of the simulation.</param>
/// <param name="PredictionQuery">The prediction query driving this simulation.</param>
/// <param name="Mode">The simulation execution mode.</param>
public record SimulationContext(
    Guid SimulationId,
    string PredictionQuery,
    SimulationMode Mode
);

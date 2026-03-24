using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Represents a simulation agent with identity, persona, and tick processing capabilities.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the unique identifier for this agent.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the JSON-serialised personality blob for this agent.
    /// </summary>
    string Persona { get; }

    /// <summary>
    /// Gets the current operational status of this agent.
    /// </summary>
    AgentStatus Status { get; }

    /// <summary>
    /// Processes a single simulation tick and returns the resulting event.
    /// </summary>
    /// <param name="tick">The simulation tick to process.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The event produced by processing the tick.</returns>
    Task<AgentEvent> ProcessTickAsync(SimulationTick tick, CancellationToken ct);
}

/// <summary>
/// Defines the operational status of an agent within a simulation.
/// </summary>
public enum AgentStatus
{
    /// <summary>Agent is idle and awaiting activation.</summary>
    Idle,

    /// <summary>Agent is actively participating in the simulation.</summary>
    Active,

    /// <summary>Agent has been suppressed and will not process ticks.</summary>
    Suppressed,

    /// <summary>Agent has been permanently terminated.</summary>
    Terminated
}

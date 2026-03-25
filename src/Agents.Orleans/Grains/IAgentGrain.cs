using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Orleans grain interface representing a single simulated agent.
/// Each grain is addressed by the agent's unique <see cref="Guid"/> key.
/// </summary>
public interface IAgentGrain : IGrainWithGuidKey, IAgent
{
    /// <summary>
    /// Initialises the agent grain with a persona and associates it with a simulation.
    /// </summary>
    /// <param name="persona">The persona definition for this agent.</param>
    /// <param name="simulationId">The simulation this agent belongs to.</param>
    Task InitialiseAsync(AgentPersona persona, Guid simulationId);

    // ProcessTickAsync is inherited from IAgent: 
    // Task<AgentEvent> ProcessTickAsync(SimulationTick tick, CancellationToken ct);

    /// <summary>
    /// Retrieves the current persona assigned to this agent.
    /// </summary>
    /// <returns>The agent's persona, or null if not yet initialised.</returns>
    Task<AgentPersona?> GetPersonaAsync();

    /// <summary>
    /// Suppresses this agent, preventing it from processing ticks until reactivated.
    /// </summary>
    Task SuppressAsync();

    /// <summary>
    /// Reactivates a previously suppressed agent, allowing it to process ticks again.
    /// </summary>
    Task ReactivateAsync();
}

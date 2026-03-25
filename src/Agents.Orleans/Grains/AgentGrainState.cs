using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Persistent state for an <see cref="IAgentGrain"/>.
/// Designed to be swappable between in-memory and durable persistence providers.
/// </summary>
[GenerateSerializer]
public class AgentGrainState
{
    /// <summary>
    /// Gets or sets the persona assigned to this agent.
    /// </summary>
    [Id(0)]
    public AgentPersona? Persona { get; set; }

    /// <summary>
    /// Gets or sets the simulation this agent belongs to.
    /// </summary>
    [Id(1)]
    public Guid SimulationId { get; set; }

    /// <summary>
    /// Gets or sets the current operational status of this agent.
    /// </summary>
    [Id(2)]
    public AgentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of ticks this agent has processed.
    /// </summary>
    [Id(3)]
    public int TicksProcessed { get; set; }
}

using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Orleans grain interface for coordinating batch processing of agent ticks.
/// Fans out tick processing across multiple agents in parallel.
/// </summary>
public interface IAgentBatchCoordinator : IGrainWithGuidKey
{
    /// <summary>
    /// Processes a simulation tick for a batch of agents in parallel.
    /// Agents are processed in configurable sub-batches to control concurrency.
    /// </summary>
    /// <param name="agentIds">The list of agent identifiers to process.</param>
    /// <param name="tick">The simulation tick to dispatch.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The list of agent events produced by all agents in the batch.</returns>
    Task<IReadOnlyList<AgentEvent>> ProcessBatchAsync(
        IReadOnlyList<Guid> agentIds,
        SimulationTick tick,
        CancellationToken ct);
}

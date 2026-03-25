namespace SwarmFish.Memory.Zep;

/// <summary>
/// Manages Zep session lifecycle for simulation agents.
/// </summary>
public interface IZepSessionManager
{
    /// <summary>
    /// Ensures a Zep session exists for the given agent. Idempotent — no-ops if session already exists.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureSessionAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the Zep session for the given agent.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteSessionAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>
    /// Batch-deletes all Zep sessions for agents in a completed simulation.
    /// </summary>
    /// <param name="simulationId">The simulation's unique identifier.</param>
    /// <param name="agentIds">The agent identifiers to purge.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PurgeSimulationAsync(Guid simulationId, IEnumerable<Guid> agentIds, CancellationToken ct = default);
}

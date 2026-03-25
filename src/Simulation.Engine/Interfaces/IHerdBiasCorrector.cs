using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Simulation.Engine.Interfaces;

/// <summary>
/// Detects and corrects "herd bias" (excessive consensus) during a simulation.
/// </summary>
public interface IHerdBiasCorrector
{
    /// <summary>
    /// Analyses the latest batch of agent events and suppresses highly aligned agents 
    /// if the diversity score falls below a threshold.
    /// </summary>
    /// <param name="batchEvents">The latest events produced by the agents.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyHerdBiasCorrectionAsync(IReadOnlyList<AgentEvent> batchEvents, CancellationToken ct);
}

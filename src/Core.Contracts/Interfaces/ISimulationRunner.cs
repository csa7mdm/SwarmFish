namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Contract for starting, controlling, and monitoring simulations.
/// </summary>
public interface ISimulationRunner
{
    /// <summary>
    /// Starts a new simulation with the given configuration.
    /// </summary>
    /// <param name="config">The simulation configuration.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The unique identifier assigned to the new simulation.</returns>
    Task<Guid> StartAsync(SimulationConfig config, CancellationToken ct);

    /// <summary>
    /// Pauses a running simulation.
    /// </summary>
    /// <param name="simulationId">The identifier of the simulation to pause.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task PauseAsync(Guid simulationId, CancellationToken ct);

    /// <summary>
    /// Resumes a previously paused simulation.
    /// </summary>
    /// <param name="simulationId">The identifier of the simulation to resume.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task ResumeAsync(Guid simulationId, CancellationToken ct);

    /// <summary>
    /// Stops a running or paused simulation permanently.
    /// </summary>
    /// <param name="simulationId">The identifier of the simulation to stop.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task StopAsync(Guid simulationId, CancellationToken ct);

    /// <summary>
    /// Streams simulation progress updates as an async enumerable.
    /// </summary>
    /// <param name="simulationId">The identifier of the simulation to watch.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>An async stream of simulation progress updates.</returns>
    IAsyncEnumerable<SimulationProgress> WatchAsync(Guid simulationId, CancellationToken ct);
}

/// <summary>
/// Configuration for a new simulation run.
/// </summary>
/// <param name="SeedDocumentId">The identifier of the seed document to use.</param>
/// <param name="AgentCount">The number of agents to spawn (2 to 100,000).</param>
/// <param name="MaxRounds">The maximum number of simulation rounds to execute.</param>
/// <param name="PredictionQuery">The prediction question the simulation should answer.</param>
/// <param name="Mode">The execution mode for the simulation.</param>
public record SimulationConfig(
    Guid SeedDocumentId,
    int AgentCount,
    int MaxRounds,
    string PredictionQuery,
    SimulationMode Mode
);

/// <summary>
/// Defines the execution mode of a simulation.
/// </summary>
public enum SimulationMode
{
    /// <summary>Standard fidelity simulation.</summary>
    Standard,

    /// <summary>High fidelity simulation with more detailed agent interactions.</summary>
    HighFidelity,

    /// <summary>Fast sweep mode for rapid hypothesis testing.</summary>
    FastSweep
}

/// <summary>
/// Represents a progress update for a running simulation.
/// </summary>
/// <param name="SimulationId">The unique identifier of the simulation.</param>
/// <param name="CurrentRound">The current round number.</param>
/// <param name="TotalRounds">The total number of rounds configured.</param>
/// <param name="State">The current state of the simulation.</param>
/// <param name="Timestamp">The timestamp of this progress update.</param>
public record SimulationProgress(
    Guid SimulationId,
    int CurrentRound,
    int TotalRounds,
    SimulationState State,
    DateTimeOffset Timestamp
);

/// <summary>
/// Defines the lifecycle state of a simulation.
/// </summary>
public enum SimulationState
{
    /// <summary>Simulation is initialising resources and agents.</summary>
    Initialising,

    /// <summary>Simulation is actively running.</summary>
    Running,

    /// <summary>Simulation has been paused.</summary>
    Paused,

    /// <summary>Simulation has completed successfully.</summary>
    Completed,

    /// <summary>Simulation has failed due to an error.</summary>
    Failed
}

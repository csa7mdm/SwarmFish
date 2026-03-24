using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Core.Domain.Events;

/// <summary>
/// Raised when a simulation starts running.
/// </summary>
/// <param name="SimulationId">The identifier of the started simulation.</param>
/// <param name="AgentCount">The number of agents participating.</param>
/// <param name="Mode">The simulation execution mode.</param>
public record SimulationStarted(Guid SimulationId, int AgentCount, SimulationMode Mode)
    : DomainEvent();

/// <summary>
/// Raised when a simulation completes successfully.
/// </summary>
/// <param name="SimulationId">The identifier of the completed simulation.</param>
/// <param name="TotalRounds">The total number of rounds executed.</param>
public record SimulationCompleted(Guid SimulationId, int TotalRounds)
    : DomainEvent();

/// <summary>
/// Raised when a simulation fails due to an error.
/// </summary>
/// <param name="SimulationId">The identifier of the failed simulation.</param>
/// <param name="Reason">The reason for the failure.</param>
public record SimulationFailed(Guid SimulationId, string Reason)
    : DomainEvent();

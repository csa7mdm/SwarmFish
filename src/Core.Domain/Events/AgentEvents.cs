namespace SwarmFish.Core.Domain.Events;

/// <summary>
/// Raised when an agent is activated within a simulation.
/// </summary>
/// <param name="AgentId">The identifier of the activated agent.</param>
/// <param name="SimulationId">The identifier of the simulation.</param>
public record AgentActivated(Guid AgentId, Guid SimulationId)
    : DomainEvent();

/// <summary>
/// Raised when an agent is suppressed within a simulation.
/// </summary>
/// <param name="AgentId">The identifier of the suppressed agent.</param>
/// <param name="SimulationId">The identifier of the simulation.</param>
/// <param name="Reason">The reason for suppression.</param>
public record AgentSuppressed(Guid AgentId, Guid SimulationId, string Reason)
    : DomainEvent();

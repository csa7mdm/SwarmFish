using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Core.Domain.Exceptions;
using SwarmFish.Core.Domain.Events;

namespace SwarmFish.Core.Domain.Entities;

/// <summary>
/// Aggregate root representing a simulation run with state machine lifecycle management.
/// </summary>
public sealed class Simulation
{
    private readonly List<Guid> _agentIds = [];
    private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>Gets the unique identifier for this simulation.</summary>
    public Guid Id { get; }

    /// <summary>Gets the simulation configuration.</summary>
    public SimulationConfig Config { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public SimulationState CurrentState { get; private set; }

    /// <summary>Gets the current round number.</summary>
    public int CurrentRound { get; private set; }

    /// <summary>Gets the list of participating agent identifiers.</summary>
    public IReadOnlyList<Guid> AgentIds => _agentIds.AsReadOnly();

    /// <summary>Gets the list of uncommitted domain events raised by this aggregate.</summary>
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Gets the timestamp when this simulation was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Initializes a new simulation in the Created state.
    /// </summary>
    /// <param name="config">The simulation configuration. Must have valid AgentCount and non-empty PredictionQuery.</param>
    /// <exception cref="DomainException">Thrown when config invariants are violated.</exception>
    public Simulation(SimulationConfig config)
    {
        ValidateConfig(config);

        Id = Guid.NewGuid();
        Config = config;
        CurrentState = SimulationState.Initialising;
        CurrentRound = 0;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds an agent to this simulation.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <exception cref="DomainException">Thrown when adding agents in an invalid state.</exception>
    public void AddAgent(Guid agentId)
    {
        if (CurrentState != SimulationState.Initialising)
            throw new DomainException("Agents can only be added during initialisation.");
        if (_agentIds.Contains(agentId))
            throw new DomainException($"Agent {agentId} is already part of this simulation.");

        _agentIds.Add(agentId);
    }

    /// <summary>
    /// Transitions the simulation to the Running state.
    /// </summary>
    /// <exception cref="DomainException">Thrown when a start transition is invalid.</exception>
    public void Start()
    {
        if (CurrentState != SimulationState.Initialising)
            throw new DomainException($"Cannot start simulation from state {CurrentState}. Must be Initialising.");

        CurrentState = SimulationState.Running;
        _domainEvents.Add(new SimulationStarted(Id, _agentIds.Count, Config.Mode));
    }

    /// <summary>
    /// Transitions the simulation to the Paused state.
    /// </summary>
    /// <exception cref="DomainException">Thrown when a pause transition is invalid.</exception>
    public void Pause()
    {
        if (CurrentState != SimulationState.Running)
            throw new DomainException($"Cannot pause simulation from state {CurrentState}. Must be Running.");

        CurrentState = SimulationState.Paused;
    }

    /// <summary>
    /// Transitions the simulation from Paused back to Running.
    /// </summary>
    /// <exception cref="DomainException">Thrown when a resume transition is invalid.</exception>
    public void Resume()
    {
        if (CurrentState != SimulationState.Paused)
            throw new DomainException($"Cannot resume simulation from state {CurrentState}. Must be Paused.");

        CurrentState = SimulationState.Running;
    }

    /// <summary>
    /// Completes a simulation round, incrementing the round counter.
    /// </summary>
    /// <param name="eventCount">The number of events produced in this round.</param>
    /// <param name="uniqueAgentCount">The number of unique agents that participated.</param>
    /// <exception cref="DomainException">Thrown when a round completes in an invalid state.</exception>
    public void CompleteRound(int eventCount, int uniqueAgentCount)
    {
        if (CurrentState != SimulationState.Running)
            throw new DomainException($"Cannot complete round from state {CurrentState}. Must be Running.");

        CurrentRound++;
        _domainEvents.Add(new RoundCompleted(Id, CurrentRound, eventCount, uniqueAgentCount));

        if (CurrentRound >= Config.MaxRounds)
        {
            Complete();
        }
    }

    /// <summary>
    /// Transitions the simulation to the Completed state.
    /// </summary>
    /// <exception cref="DomainException">Thrown when a complete transition is invalid.</exception>
    public void Complete()
    {
        if (CurrentState != SimulationState.Running)
            throw new DomainException($"Cannot complete simulation from state {CurrentState}. Must be Running.");

        CurrentState = SimulationState.Completed;
        _domainEvents.Add(new SimulationCompleted(Id, CurrentRound));
    }

    /// <summary>
    /// Transitions the simulation to the Failed state.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <exception cref="DomainException">Thrown when a fail transition is invalid.</exception>
    public void Fail(string reason)
    {
        if (CurrentState is SimulationState.Completed or SimulationState.Failed)
            throw new DomainException($"Cannot fail simulation from terminal state {CurrentState}.");

        CurrentState = SimulationState.Failed;
        _domainEvents.Add(new SimulationFailed(Id, reason));
    }

    /// <summary>
    /// Clears uncommitted domain events (after they have been dispatched).
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private static void ValidateConfig(SimulationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.AgentCount < 2 || config.AgentCount > 100_000)
            throw new DomainException("AgentCount must be between 2 and 100,000.");
        if (config.MaxRounds < 1)
            throw new DomainException("MaxRounds must be at least 1.");
        if (string.IsNullOrWhiteSpace(config.PredictionQuery))
            throw new DomainException("PredictionQuery must not be empty.");
    }
}

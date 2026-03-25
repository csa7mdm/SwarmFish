using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Domain.Entities;
using SwarmFish.Core.Domain.Events;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Tests.Unit.Core.Domain;

/// <summary>
/// Tests for <see cref="Simulation"/> aggregate root state machine.
/// </summary>
public class SimulationTests
{
    private static SimulationConfig ValidConfig => new(
        SeedDocumentId: Guid.NewGuid(),
        AgentCount: 10,
        MaxRounds: 5,
        PredictionQuery: "What happens next?",
        Mode: SimulationMode.Standard
    );

    [Fact]
    public void Constructor_ValidConfig_CreatesSimulationInInitialisingState()
    {
        var sim = new Simulation(ValidConfig);

        Assert.Equal(SimulationState.Initialising, sim.CurrentState);
        Assert.Equal(0, sim.CurrentRound);
        Assert.Empty(sim.AgentIds);
    }

    [Fact]
    public void Start_FromInitialising_TransitionsToRunning()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();

        Assert.Equal(SimulationState.Running, sim.CurrentState);
        Assert.Contains(sim.DomainEvents, e => e is SimulationStarted);
    }

    [Fact]
    public void Pause_FromRunning_TransitionsToPaused()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Pause();

        Assert.Equal(SimulationState.Paused, sim.CurrentState);
    }

    [Fact]
    public void Resume_FromPaused_TransitionsToRunning()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Pause();
        sim.Resume();

        Assert.Equal(SimulationState.Running, sim.CurrentState);
    }

    [Fact]
    public void Complete_FromRunning_TransitionsToCompleted()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Complete();

        Assert.Equal(SimulationState.Completed, sim.CurrentState);
        Assert.Contains(sim.DomainEvents, e => e is SimulationCompleted);
    }

    [Fact]
    public void Fail_FromRunning_TransitionsToFailed()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Fail("test error");

        Assert.Equal(SimulationState.Failed, sim.CurrentState);
        Assert.Contains(sim.DomainEvents, e => e is SimulationFailed);
    }

    [Fact]
    public void Fail_FromInitialising_TransitionsToFailed()
    {
        var sim = new Simulation(ValidConfig);
        sim.Fail("startup error");

        Assert.Equal(SimulationState.Failed, sim.CurrentState);
    }

    [Fact]
    public void Fail_FromPaused_TransitionsToFailed()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Pause();
        sim.Fail("paused failure");

        Assert.Equal(SimulationState.Failed, sim.CurrentState);
    }

    // Invalid transitions
    [Fact]
    public void Start_FromRunning_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();

        Assert.Throws<DomainException>(() => sim.Start());
    }

    [Fact]
    public void Pause_FromInitialising_Throws()
    {
        var sim = new Simulation(ValidConfig);

        Assert.Throws<DomainException>(() => sim.Pause());
    }

    [Fact]
    public void Resume_FromRunning_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();

        Assert.Throws<DomainException>(() => sim.Resume());
    }

    [Fact]
    public void Complete_FromPaused_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Pause();

        Assert.Throws<DomainException>(() => sim.Complete());
    }

    [Fact]
    public void Fail_FromCompleted_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.Complete();

        Assert.Throws<DomainException>(() => sim.Fail("should not work"));
    }

    [Fact]
    public void Fail_FromFailed_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Fail("first");

        Assert.Throws<DomainException>(() => sim.Fail("second"));
    }

    [Fact]
    public void AddAgent_DuringInitialisation_Succeeds()
    {
        var sim = new Simulation(ValidConfig);
        var agentId = Guid.NewGuid();
        sim.AddAgent(agentId);

        Assert.Contains(agentId, sim.AgentIds);
    }

    [Fact]
    public void AddAgent_DuplicateId_Throws()
    {
        var sim = new Simulation(ValidConfig);
        var agentId = Guid.NewGuid();
        sim.AddAgent(agentId);

        Assert.Throws<DomainException>(() => sim.AddAgent(agentId));
    }

    [Fact]
    public void AddAgent_WhenRunning_Throws()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();

        Assert.Throws<DomainException>(() => sim.AddAgent(Guid.NewGuid()));
    }

    [Fact]
    public void CompleteRound_IncrementsRoundAndRaisesEvent()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        sim.CompleteRound(eventCount: 10, uniqueAgentCount: 5);

        Assert.Equal(1, sim.CurrentRound);
        Assert.Contains(sim.DomainEvents, e => e is RoundCompleted);
    }

    [Fact]
    public void CompleteRound_ReachMaxRounds_AutoCompletes()
    {
        var config = ValidConfig with { MaxRounds = 2 };
        var sim = new Simulation(config);
        sim.Start();
        sim.CompleteRound(5, 3);
        sim.CompleteRound(5, 3);

        Assert.Equal(SimulationState.Completed, sim.CurrentState);
        Assert.Equal(2, sim.CurrentRound);
    }

    [Fact]
    public void ClearDomainEvents_ClearsAll()
    {
        var sim = new Simulation(ValidConfig);
        sim.Start();
        Assert.NotEmpty(sim.DomainEvents);

        sim.ClearDomainEvents();
        Assert.Empty(sim.DomainEvents);
    }
}

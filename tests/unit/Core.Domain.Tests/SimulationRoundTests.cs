using SwarmFish.Core.Contracts.Models;
using SwarmFish.Core.Domain.Exceptions;
using SwarmFish.Core.Domain.ValueObjects;

namespace SwarmFish.Tests.Unit.Core.Domain;

/// <summary>
/// Tests for <see cref="SimulationRound"/> value object.
/// </summary>
public class SimulationRoundTests
{
    private static AgentEvent MakeEvent(Guid agentId, string eventType) =>
        new(agentId, eventType, "{}", DateTimeOffset.UtcNow);

    [Fact]
    public void Constructor_ValidInputs_ComputesStats()
    {
        var agent1 = Guid.NewGuid();
        var agent2 = Guid.NewGuid();
        var events = new List<AgentEvent>
        {
            MakeEvent(agent1, "spoke"),
            MakeEvent(agent1, "spoke"),
            MakeEvent(agent2, "reacted"),
        };

        var round = new SimulationRound(1, events, TimeSpan.FromSeconds(2));

        Assert.Equal(1, round.RoundNumber);
        Assert.Equal(3, round.EventCount);
        Assert.Equal(2, round.UniqueAgents);
        Assert.Equal("spoke", round.DominantEventType);
        Assert.Equal(TimeSpan.FromSeconds(2), round.Duration);
    }

    [Fact]
    public void Constructor_EmptyEvents_DominantTypeIsEmpty()
    {
        var round = new SimulationRound(1, new List<AgentEvent>(), TimeSpan.Zero);

        Assert.Equal(0, round.EventCount);
        Assert.Equal(0, round.UniqueAgents);
        Assert.Equal(string.Empty, round.DominantEventType);
    }

    [Fact]
    public void Constructor_RoundNumberZero_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new SimulationRound(0, new List<AgentEvent>(), TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeDuration_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new SimulationRound(1, new List<AgentEvent>(), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_SingleEventType_DominatesThat()
    {
        var events = new List<AgentEvent>
        {
            MakeEvent(Guid.NewGuid(), "silent"),
            MakeEvent(Guid.NewGuid(), "silent"),
        };

        var round = new SimulationRound(1, events, TimeSpan.FromMilliseconds(500));

        Assert.Equal("silent", round.DominantEventType);
    }
}

using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Domain.Entities;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Tests.Unit.Core.Domain;

/// <summary>
/// Tests for <see cref="SimulationConfig"/> validation via Simulation constructor.
/// </summary>
public class SimulationConfigTests
{
    [Fact]
    public void Config_AgentCountBelowMinimum_Throws()
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 1, MaxRounds: 5, "query", SimulationMode.Standard);

        Assert.Throws<DomainException>(() => new Simulation(config));
    }

    [Fact]
    public void Config_AgentCountAboveMaximum_Throws()
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 100_001, MaxRounds: 5, "query", SimulationMode.Standard);

        Assert.Throws<DomainException>(() => new Simulation(config));
    }

    [Fact]
    public void Config_AgentCountAtMinimum_Succeeds()
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 2, MaxRounds: 5, "query", SimulationMode.Standard);
        var sim = new Simulation(config);

        Assert.Equal(2, sim.Config.AgentCount);
    }

    [Fact]
    public void Config_AgentCountAtMaximum_Succeeds()
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 100_000, MaxRounds: 5, "query", SimulationMode.Standard);
        var sim = new Simulation(config);

        Assert.Equal(100_000, sim.Config.AgentCount);
    }

    [Fact]
    public void Config_MaxRoundsZero_Throws()
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 10, MaxRounds: 0, "query", SimulationMode.Standard);

        Assert.Throws<DomainException>(() => new Simulation(config));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Config_EmptyPredictionQuery_Throws(string? query)
    {
        var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 10, MaxRounds: 5, query!, SimulationMode.Standard);

        Assert.Throws<DomainException>(() => new Simulation(config));
    }

    [Fact]
    public void Config_AllModes_Succeed()
    {
        foreach (var mode in Enum.GetValues<SimulationMode>())
        {
            var config = new SimulationConfig(Guid.NewGuid(), AgentCount: 10, MaxRounds: 5, "query", mode);
            var sim = new Simulation(config);

            Assert.Equal(mode, sim.Config.Mode);
        }
    }
}

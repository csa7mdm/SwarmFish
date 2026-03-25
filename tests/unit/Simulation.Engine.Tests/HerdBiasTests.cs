using Microsoft.Extensions.Logging;
using Moq;
using Orleans;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Simulation.Engine.Services;
using Xunit;

namespace Simulation.Engine.Tests;

public class HerdBiasTests
{
    private readonly Mock<IGrainFactory> _grainFactoryMock;
    private readonly Mock<ILogger<HerdBiasCorrector>> _loggerMock;
    private readonly HerdBiasCorrector _corrector;

    public HerdBiasTests()
    {
        _grainFactoryMock = new Mock<IGrainFactory>();
        _loggerMock = new Mock<ILogger<HerdBiasCorrector>>();
        _corrector = new HerdBiasCorrector(_grainFactoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ApplyHerdBiasCorrectionAsync_HighDiversity_DoesNotSuppress()
    {
        // 10 events, 4 types => diversity 0.4 (threshold is 0.3)
        var events = new List<AgentEvent>();
        for (int i = 0; i < 4; i++) events.Add(new AgentEvent(Guid.NewGuid(), "type1", "", DateTimeOffset.UtcNow));
        for (int i = 0; i < 2; i++) events.Add(new AgentEvent(Guid.NewGuid(), "type2", "", DateTimeOffset.UtcNow));
        for (int i = 0; i < 2; i++) events.Add(new AgentEvent(Guid.NewGuid(), "type3", "", DateTimeOffset.UtcNow));
        for (int i = 0; i < 2; i++) events.Add(new AgentEvent(Guid.NewGuid(), "type4", "", DateTimeOffset.UtcNow));

        await _corrector.ApplyHerdBiasCorrectionAsync(events, CancellationToken.None);

        _grainFactoryMock.Verify(g => g.GetGrain<IAgentGrain>(It.IsAny<Guid>(), null), Times.Never);
    }

    [Fact]
    public async Task ApplyHerdBiasCorrectionAsync_LowDiversity_SuppressesDominantGroup()
    {
        // 10 events, 2 types => diversity 0.2 (threshold is 0.3)
        // Dominant group has 8 agents, so 20% suppression = 1 agent suppressed
        // 1.6 truncated to int is 1. Math.Max(1, ...)
        
        var dominantIds = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();
        var events = dominantIds.Select(id => new AgentEvent(id, "same_type", "", DateTimeOffset.UtcNow)).ToList();
        events.Add(new AgentEvent(Guid.NewGuid(), "other_type", "", DateTimeOffset.UtcNow));
        events.Add(new AgentEvent(Guid.NewGuid(), "other_type", "", DateTimeOffset.UtcNow));

        var suppressedAgents = new List<Guid>();

        foreach (var id in dominantIds)
        {
            var grainMock = new Mock<IAgentGrain>();
            grainMock.Setup(g => g.SuppressAsync())
                .Callback(() => suppressedAgents.Add(id))
                .Returns(Task.CompletedTask);

            _grainFactoryMock.Setup(g => g.GetGrain<IAgentGrain>(id, null)).Returns(grainMock.Object);
        }

        await _corrector.ApplyHerdBiasCorrectionAsync(events, CancellationToken.None);

        // 8 * 0.2 = 1.6 -> cast to int = 1. Verify 1 agent is suppressed.
        Assert.Single(suppressedAgents);
        Assert.Contains(suppressedAgents[0], dominantIds); // Ensure it suppressed someone from the dominant group
    }
}

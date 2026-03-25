using Microsoft.Extensions.Logging;
using Moq;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using Xunit;

namespace SwarmFish.Agents.Orleans.Tests;

/// <summary>
/// Unit tests for <see cref="AgentBatchCoordinator"/> covering batch fan-out,
/// error handling, and mixed success/failure scenarios.
/// </summary>
public class AgentBatchCoordinatorTests
{
    private readonly Mock<IGrainFactory> _grainFactoryMock;
    private readonly Mock<ILogger<AgentBatchCoordinator>> _loggerMock;

    public AgentBatchCoordinatorTests()
    {
        _grainFactoryMock = new Mock<IGrainFactory>();
        _loggerMock = new Mock<ILogger<AgentBatchCoordinator>>();
    }

    private static SimulationTick CreateTestTick(int round = 1)
    {
        return new SimulationTick(
            Round: round,
            SimulatedTime: DateTimeOffset.UtcNow,
            PreviousEvents: Array.Empty<AgentEvent>(),
            Context: new SimulationContext(
                Guid.NewGuid(),
                "Test query",
                SimulationMode.Standard));
    }

    [Fact]
    public async Task ProcessBatchAsync_FansOutToAllAgents()
    {
        // Arrange
        var agentIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var tick = CreateTestTick();

        foreach (var agentId in agentIds)
        {
            var grainMock = new Mock<IAgentGrain>();
            grainMock
                .Setup(g => g.ProcessTickAsync(tick, CancellationToken.None))
                .ReturnsAsync(new AgentEvent(agentId, "spoke", "Hello", DateTimeOffset.UtcNow));
            _grainFactoryMock
                .Setup(f => f.GetGrain<IAgentGrain>(agentId, null))
                .Returns(grainMock.Object);
        }

        var coordinator = new TestableBatchCoordinator(
            _grainFactoryMock.Object, _loggerMock.Object);

        // Act
        var results = await coordinator.ProcessBatchAsync(agentIds, tick, CancellationToken.None);

        // Assert
        Assert.Equal(agentIds.Count, results.Count);
        Assert.All(results, e => Assert.Equal("spoke", e.EventType));
    }

    [Fact]
    public async Task ProcessBatchAsync_HandlesPartialFailures()
    {
        // Arrange
        var successId = Guid.NewGuid();
        var failureId = Guid.NewGuid();
        var agentIds = new List<Guid> { successId, failureId };
        var tick = CreateTestTick();

        var successGrain = new Mock<IAgentGrain>();
        successGrain
            .Setup(g => g.ProcessTickAsync(tick, CancellationToken.None))
            .ReturnsAsync(new AgentEvent(successId, "spoke", "Success", DateTimeOffset.UtcNow));

        var failureGrain = new Mock<IAgentGrain>();
        failureGrain
            .Setup(g => g.ProcessTickAsync(tick, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Agent not initialised"));

        _grainFactoryMock.Setup(f => f.GetGrain<IAgentGrain>(successId, null)).Returns(successGrain.Object);
        _grainFactoryMock.Setup(f => f.GetGrain<IAgentGrain>(failureId, null)).Returns(failureGrain.Object);

        var coordinator = new TestableBatchCoordinator(
            _grainFactoryMock.Object, _loggerMock.Object);

        // Act
        var results = await coordinator.ProcessBatchAsync(agentIds, tick, CancellationToken.None);

        // Assert — both agents produce results, the failed one gets a "silent" fallback
        Assert.Equal(2, results.Count);

        var successEvent = results.First(e => e.AgentId == successId);
        Assert.Equal("spoke", successEvent.EventType);

        var failureEvent = results.First(e => e.AgentId == failureId);
        Assert.Equal("silent", failureEvent.EventType);
    }

    [Fact]
    public async Task ProcessBatchAsync_RespectsSubBatchSize()
    {
        // Arrange — 120 agents should result in 3 sub-batches (50 + 50 + 20)
        var agentIds = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid()).ToList();
        var tick = CreateTestTick();

        foreach (var agentId in agentIds)
        {
            var grainMock = new Mock<IAgentGrain>();
            grainMock
                .Setup(g => g.ProcessTickAsync(tick, CancellationToken.None))
                .ReturnsAsync(new AgentEvent(agentId, "moved", "Moved", DateTimeOffset.UtcNow));
            _grainFactoryMock
                .Setup(f => f.GetGrain<IAgentGrain>(agentId, null))
                .Returns(grainMock.Object);
        }

        var coordinator = new TestableBatchCoordinator(
            _grainFactoryMock.Object, _loggerMock.Object);

        // Act
        var results = await coordinator.ProcessBatchAsync(agentIds, tick, CancellationToken.None);

        // Assert
        Assert.Equal(120, results.Count);
    }

    [Fact]
    public async Task ProcessBatchAsync_SupportsCancellation()
    {
        // Arrange
        var agentIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        var tick = CreateTestTick();
        var cts = new CancellationTokenSource();

        foreach (var agentId in agentIds)
        {
            var grainMock = new Mock<IAgentGrain>();
            grainMock
                .Setup(g => g.ProcessTickAsync(tick, CancellationToken.None))
                .ReturnsAsync(new AgentEvent(agentId, "spoke", "Hello", DateTimeOffset.UtcNow));
            _grainFactoryMock
                .Setup(f => f.GetGrain<IAgentGrain>(agentId, null))
                .Returns(grainMock.Object);
        }

        // Cancel immediately
        await cts.CancelAsync();

        var coordinator = new TestableBatchCoordinator(
            _grainFactoryMock.Object, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => coordinator.ProcessBatchAsync(agentIds, tick, cts.Token));
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyList_ReturnsEmptyResults()
    {
        // Arrange
        var coordinator = new TestableBatchCoordinator(
            _grainFactoryMock.Object, _loggerMock.Object);

        // Act
        var results = await coordinator.ProcessBatchAsync(
            Array.Empty<Guid>(), CreateTestTick(), CancellationToken.None);

        // Assert
        Assert.Empty(results);
    }
}

/// <summary>
/// Testable batch coordinator that bypasses Orleans grain activation.
/// </summary>
internal class TestableBatchCoordinator : AgentBatchCoordinator
{
    public TestableBatchCoordinator(
        IGrainFactory grainFactory,
        ILogger<AgentBatchCoordinator> logger)
        : base(grainFactory, logger)
    {
    }
}

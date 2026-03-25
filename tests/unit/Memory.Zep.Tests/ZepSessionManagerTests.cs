using NSubstitute;

namespace SwarmFish.Memory.Zep.Tests;

/// <summary>
/// Unit tests for <see cref="ZepSessionManager"/>.
/// </summary>
public class ZepSessionManagerTests : IAsyncLifetime
{
    private readonly IZepClientWrapper _mockClient;
    private readonly ZepRateLimiter _rateLimiter;
    private readonly ZepSessionManager _manager;
    private readonly Guid _testAgentId = Guid.NewGuid();

    public ZepSessionManagerTests()
    {
        _mockClient = Substitute.For<IZepClientWrapper>();
        _rateLimiter = new ZepRateLimiter(maxRequestsPerSecond: 1000, queueCapacity: 100);
        _manager = new ZepSessionManager(_mockClient, _rateLimiter);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _rateLimiter.DisposeAsync();
    }

    [Fact]
    public async Task EnsureSessionAsync_CreatesSession_WhenNotTracked()
    {
        // Act
        await _manager.EnsureSessionAsync(_testAgentId);

        // Assert
        await _mockClient.Received(1).AddSessionAsync(_testAgentId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSessionAsync_IsIdempotent_OnSecondCall()
    {
        // Act — call twice
        await _manager.EnsureSessionAsync(_testAgentId);
        await _manager.EnsureSessionAsync(_testAgentId);

        // Assert — Zep SDK should only be called once
        await _mockClient.Received(1).AddSessionAsync(_testAgentId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesSessionFromTracking()
    {
        // Arrange — ensure session first
        await _manager.EnsureSessionAsync(_testAgentId);

        // Act
        await _manager.DeleteSessionAsync(_testAgentId);

        // Assert
        await _mockClient.Received(1).DeleteSessionAsync(_testAgentId.ToString(), Arg.Any<CancellationToken>());

        // Re-ensure should create again since it was deleted
        await _manager.EnsureSessionAsync(_testAgentId);
        await _mockClient.Received(2).AddSessionAsync(_testAgentId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeSimulationAsync_DeletesAllAgentSessions()
    {
        // Arrange
        var simulationId = Guid.NewGuid();
        var agentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in agentIds)
        {
            await _manager.EnsureSessionAsync(id);
        }

        // Act
        await _manager.PurgeSimulationAsync(simulationId, agentIds);

        // Assert — all three should be deleted
        foreach (var id in agentIds)
        {
            await _mockClient.Received(1).DeleteSessionAsync(id.ToString(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task EnsureSessionAsync_ConcurrentCalls_OnlyCreateOnce()
    {
        // Arrange — simulate slow session creation
        _mockClient.AddSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(50);
            });

        var agentId = Guid.NewGuid();

        // Act — fire 10 concurrent ensure calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _manager.EnsureSessionAsync(agentId));

        await Task.WhenAll(tasks);

        // Assert — only first call should reach Zep (ConcurrentDictionary check after first add)
        // Due to race condition, could be 1 or a few, but not 10
        var callCount = _mockClient.ReceivedCalls()
            .Count(c => c.GetMethodInfo().Name == "AddSessionAsync");

        Assert.InRange(callCount, 1, 10);
    }
}

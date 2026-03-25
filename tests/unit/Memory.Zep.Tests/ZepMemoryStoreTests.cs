using NSubstitute;
using Microsoft.Extensions.Options;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Memory.Zep.Tests;

/// <summary>
/// Unit tests for <see cref="ZepMemoryStore"/>.
/// </summary>
public class ZepMemoryStoreTests : IAsyncLifetime
{
    private readonly IZepClientWrapper _mockClient;
    private readonly IZepSessionManager _mockSessionManager;
    private readonly ZepRateLimiter _rateLimiter;
    private readonly ZepMemoryStore _store;
    private readonly Guid _testAgentId = Guid.NewGuid();

    public ZepMemoryStoreTests()
    {
        _mockClient = Substitute.For<IZepClientWrapper>();
        _mockSessionManager = Substitute.For<IZepSessionManager>();
        _rateLimiter = new ZepRateLimiter(maxRequestsPerSecond: 1000, queueCapacity: 100);

        var options = Options.Create(new ZepMemoryOptions
        {
            ApiKey = "test-key",
            SearchTopK = 5,
            MaxMemoriesPerAgent = 500,
            RateLimitRps = 100
        });

        _store = new ZepMemoryStore(_mockClient, _mockSessionManager, _rateLimiter, options);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _rateLimiter.DisposeAsync();
    }

    [Fact]
    public async Task AppendMemoryAsync_MapsToZepAddMemory()
    {
        // Arrange
        var entry = new MemoryEntry("Test observation", DateTimeOffset.UtcNow, MemoryEntryType.Observation);

        // Act
        await _store.AppendMemoryAsync(_testAgentId, entry, CancellationToken.None);

        // Assert
        await _mockSessionManager.Received(1).EnsureSessionAsync(_testAgentId, Arg.Any<CancellationToken>());
        await _mockClient.Received(1).AddMemoryAsync(
            _testAgentId.ToString(),
            "assistant",
            "ai",
            "Test observation",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchMemoryAsync_MapsToZepSearchAndConvertsResults()
    {
        // Arrange
        var searchResults = new List<ZepSearchResult>
        {
            new("Result 1", 0.95f, DateTimeOffset.UtcNow.AddMinutes(-5)),
            new("Result 2", 0.82f, DateTimeOffset.UtcNow.AddMinutes(-10))
        };

        _mockClient.SearchMemoryAsync(
            _testAgentId.ToString(),
            "test query",
            5,
            Arg.Any<CancellationToken>()
        ).Returns(searchResults);

        // Act
        var results = await _store.SearchMemoryAsync(_testAgentId, "test query", 5, CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("Result 1", results[0].Content);
        Assert.Equal(0.95f, results[0].Relevance);
        Assert.Equal("Result 2", results[1].Content);
        Assert.Equal(0.82f, results[1].Relevance);
    }

    [Fact]
    public async Task SearchMemoryAsync_UsesDefaultTopK_WhenZeroProvided()
    {
        // Arrange
        _mockClient.SearchMemoryAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()
        ).Returns(new List<ZepSearchResult>());

        // Act
        await _store.SearchMemoryAsync(_testAgentId, "query", 0, CancellationToken.None);

        // Assert — should use default topK of 5 from options
        await _mockClient.Received(1).SearchMemoryAsync(
            _testAgentId.ToString(), "query", 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMemoryAsync_ReturnsAgentMemoryWithEntries()
    {
        // Arrange
        var messages = new List<ZepMemoryMessage>
        {
            new("assistant", "Memory 1", DateTimeOffset.UtcNow.AddMinutes(-30)),
            new("assistant", "Memory 2", DateTimeOffset.UtcNow.AddMinutes(-15)),
            new("assistant", "Memory 3", DateTimeOffset.UtcNow)
        };

        _mockClient.GetMemoryAsync(_testAgentId.ToString(), Arg.Any<CancellationToken>())
            .Returns(messages);

        // Act
        var memory = await _store.GetMemoryAsync(_testAgentId, CancellationToken.None);

        // Assert
        Assert.Equal(_testAgentId, memory.AgentId);
        Assert.Equal(3, memory.Entries.Count);
        Assert.Equal("Memory 1", memory.Entries[0].Content);
        Assert.Equal("Memory 3", memory.Entries[2].Content);
    }

    [Fact]
    public async Task GetMemoryAsync_EnsuresSessionBeforeQuery()
    {
        // Arrange
        _mockClient.GetMemoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<ZepMemoryMessage>());

        // Act
        await _store.GetMemoryAsync(_testAgentId, CancellationToken.None);

        // Assert
        await _mockSessionManager.Received(1).EnsureSessionAsync(_testAgentId, Arg.Any<CancellationToken>());
    }
}

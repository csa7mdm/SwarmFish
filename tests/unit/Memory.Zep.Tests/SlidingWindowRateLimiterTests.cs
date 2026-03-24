namespace SwarmFish.Memory.Zep.Tests;

/// <summary>
/// Unit tests for <see cref="ZepRateLimiter"/>.
/// </summary>
public class SlidingWindowRateLimiterTests : IAsyncLifetime
{
    private ZepRateLimiter _rateLimiter = null!;

    public Task InitializeAsync()
    {
        _rateLimiter = new ZepRateLimiter(maxRequestsPerSecond: 100, queueCapacity: 1000);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _rateLimiter.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsResult_WhenWithinRateLimit()
    {
        // Act
        var result = await _rateLimiter.ExecuteAsync(() => Task.FromResult(42));

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_CompletesSuccessfully()
    {
        // Arrange
        var executed = false;

        // Act
        await _rateLimiter.ExecuteAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMultipleConcurrentRequests()
    {
        // Arrange — 50 concurrent requests should all succeed within rate limit
        var counter = 0;
        var tasks = Enumerable.Range(0, 50).Select(_ =>
            _rateLimiter.ExecuteAsync(() =>
            {
                Interlocked.Increment(ref counter);
                return Task.FromResult(true);
            }));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(50, counter);
    }

    [Fact]
    public async Task ExecuteAsync_AllRequestsComplete_Under200ConcurrentLoad()
    {
        // Arrange — 200 concurrent requests against 100 rps limit
        // All should eventually complete (some via overflow channel)
        var counter = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var tasks = Enumerable.Range(0, 200).Select(_ =>
            _rateLimiter.ExecuteAsync(() =>
            {
                Interlocked.Increment(ref counter);
                return Task.FromResult(true);
            }, cts.Token));

        // Act
        await Task.WhenAll(tasks);

        // Assert — all 200 requests should complete
        Assert.Equal(200, counter);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesExceptions()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rateLimiter.ExecuteAsync<int>(() =>
                throw new InvalidOperationException("Test error")));
    }

    [Fact]
    public async Task DisposeAsync_CompletesGracefully()
    {
        // Arrange
        var limiter = new ZepRateLimiter(maxRequestsPerSecond: 100, queueCapacity: 100);

        // Act — should not throw
        await limiter.DisposeAsync();
    }
}

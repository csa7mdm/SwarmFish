using System.Threading.Channels;
using System.Threading.RateLimiting;

namespace SwarmFish.Memory.Zep;

/// <summary>
/// A sliding-window rate limiter that enforces a maximum requests-per-second limit
/// across all agents. Overflow requests are queued with backpressure via a bounded channel.
/// </summary>
public sealed class ZepRateLimiter : IAsyncDisposable
{
    private readonly System.Threading.RateLimiting.SlidingWindowRateLimiter _limiter;
    private readonly Channel<Func<Task>> _overflowChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZepRateLimiter"/> class.
    /// </summary>
    /// <param name="maxRequestsPerSecond">Maximum requests per second.</param>
    /// <param name="queueCapacity">Bounded channel capacity for overflow.</param>
    public ZepRateLimiter(int maxRequestsPerSecond = 100, int queueCapacity = 1000)
    {
        _limiter = new System.Threading.RateLimiting.SlidingWindowRateLimiter(
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = maxRequestsPerSecond,
                Window = TimeSpan.FromSeconds(1),
                SegmentsPerWindow = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0, // We handle queueing ourselves via the channel
                AutoReplenishment = true
            });

        _overflowChannel = Channel.CreateBounded<Func<Task>>(
            new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        _drainTask = Task.Run(DrainOverflowAsync);
    }

    /// <summary>
    /// Executes a function through the rate limiter, queueing if at capacity.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The async function to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the function.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> func, CancellationToken ct = default)
    {
        using var lease = await _limiter.AcquireAsync(1, ct).ConfigureAwait(false);

        if (lease.IsAcquired)
        {
            return await func().ConfigureAwait(false);
        }

        // Rate limit exceeded — queue the work via the overflow channel
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _overflowChannel.Writer.WriteAsync(async () =>
        {
            try
            {
                // Wait for a lease in the overflow drain loop
                using var retryLease = await _limiter.AcquireAsync(1, ct).ConfigureAwait(false);
                if (!retryLease.IsAcquired)
                {
                    tcs.SetException(new InvalidOperationException("Rate limiter lease could not be acquired."));
                    return;
                }

                var result = await func().ConfigureAwait(false);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, ct).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a void action through the rate limiter, queueing if at capacity.
    /// </summary>
    /// <param name="func">The async action to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(Func<Task> func, CancellationToken ct = default)
    {
        await ExecuteAsync(async () =>
        {
            await func().ConfigureAwait(false);
            return 0; // dummy return
        }, ct).ConfigureAwait(false);
    }

    private async Task DrainOverflowAsync()
    {
        try
        {
            await foreach (var work in _overflowChannel.Reader.ReadAllAsync(_cts.Token))
            {
                await work().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Disposes the rate limiter and drains any pending overflow items.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _overflowChannel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _drainTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _limiter.Dispose();
        _cts.Dispose();
    }
}

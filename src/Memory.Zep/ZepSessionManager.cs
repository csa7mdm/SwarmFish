using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace SwarmFish.Memory.Zep;

/// <summary>
/// Manages Zep session lifecycle with idempotent creation tracking.
/// </summary>
public class ZepSessionManager : IZepSessionManager
{
    private readonly IZepClientWrapper _client;
    private readonly ZepRateLimiter _rateLimiter;
    private readonly ConcurrentDictionary<Guid, bool> _activeSessions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ZepSessionManager"/> class.
    /// </summary>
    /// <param name="client">The Zep client wrapper.</param>
    /// <param name="rateLimiter">The rate limiter instance.</param>
    public ZepSessionManager(IZepClientWrapper client, ZepRateLimiter rateLimiter)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    /// <inheritdoc />
    public async Task EnsureSessionAsync(Guid agentId, CancellationToken ct = default)
    {
        if (_activeSessions.ContainsKey(agentId))
        {
            return; // Already tracked — idempotent
        }

        await _rateLimiter.ExecuteAsync(async () =>
        {
            var sessionId = agentId.ToString();
            await _client.AddSessionAsync(sessionId, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        _activeSessions.TryAdd(agentId, true);
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(Guid agentId, CancellationToken ct = default)
    {
        var sessionId = agentId.ToString();
        await _rateLimiter.ExecuteAsync(async () =>
        {
            await _client.DeleteSessionAsync(sessionId, ct).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        _activeSessions.TryRemove(agentId, out _);
    }

    /// <inheritdoc />
    public async Task PurgeSimulationAsync(Guid simulationId, IEnumerable<Guid> agentIds, CancellationToken ct = default)
    {
        var deleteTasks = agentIds.Select(id => DeleteSessionAsync(id, ct));
        await Task.WhenAll(deleteTasks).ConfigureAwait(false);
    }
}

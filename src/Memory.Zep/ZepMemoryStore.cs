using Microsoft.Extensions.Options;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Memory.Zep;

/// <summary>
/// Implements <see cref="IMemoryStore"/> by adapting Zep Cloud's session-based memory API.
/// All API calls are throttled through a sliding-window rate limiter.
/// </summary>
public class ZepMemoryStore : IMemoryStore
{
    private readonly IZepClientWrapper _client;
    private readonly IZepSessionManager _sessionManager;
    private readonly ZepRateLimiter _rateLimiter;
    private readonly ZepMemoryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZepMemoryStore"/> class.
    /// </summary>
    /// <param name="client">The Zep client wrapper.</param>
    /// <param name="sessionManager">The session manager for lifecycle operations.</param>
    /// <param name="rateLimiter">The rate limiter for API throttling.</param>
    /// <param name="options">Configuration options.</param>
    public ZepMemoryStore(
        IZepClientWrapper client,
        IZepSessionManager sessionManager,
        ZepRateLimiter rateLimiter,
        IOptions<ZepMemoryOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<AgentMemory> GetMemoryAsync(Guid agentId, CancellationToken ct)
    {
        await _sessionManager.EnsureSessionAsync(agentId, ct).ConfigureAwait(false);

        var sessionId = agentId.ToString();
        var messages = await _rateLimiter.ExecuteAsync(
            () => _client.GetMemoryAsync(sessionId, ct), ct).ConfigureAwait(false);

        var entries = messages.Select(m => new MemoryEntry(
            Content: m.Content,
            CreatedAt: m.CreatedAt,
            Type: MemoryEntryType.Observation,
            Relevance: 1.0f
        )).ToList();

        return new AgentMemory(agentId, entries);
    }

    /// <inheritdoc />
    public async Task AppendMemoryAsync(Guid agentId, MemoryEntry entry, CancellationToken ct)
    {
        await _sessionManager.EnsureSessionAsync(agentId, ct).ConfigureAwait(false);

        var sessionId = agentId.ToString();
        await _rateLimiter.ExecuteAsync(
            () => _client.AddMemoryAsync(sessionId, "assistant", "ai", entry.Content, ct), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(Guid agentId, string query, int topK, CancellationToken ct)
    {
        await _sessionManager.EnsureSessionAsync(agentId, ct).ConfigureAwait(false);

        var sessionId = agentId.ToString();
        var effectiveTopK = topK > 0 ? topK : _options.SearchTopK;

        var results = await _rateLimiter.ExecuteAsync(
            () => _client.SearchMemoryAsync(sessionId, query, effectiveTopK, ct), ct).ConfigureAwait(false);

        return results.Select(r => new MemoryEntry(
            Content: r.Content,
            CreatedAt: r.CreatedAt,
            Type: MemoryEntryType.Observation,
            Relevance: r.Score
        )).ToList();
    }
}

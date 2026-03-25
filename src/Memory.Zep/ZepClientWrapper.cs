using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Memory.Zep;

/// <summary>
/// Stub implementation of Zep client wrapper for API Gateway compilation.
/// In a real system, this would call the Zep Cloud API.
/// </summary>
public class ZepClientWrapper : IZepClientWrapper
{
    /// <inheritdoc />
    public Task AddMemoryAsync(string sessionId, string role, string roleType, string content, CancellationToken ct = default) 
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task AddSessionAsync(string sessionId, CancellationToken ct = default) 
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default) 
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<ZepMemoryMessage>> GetMemoryAsync(string sessionId, CancellationToken ct = default) 
        => Task.FromResult<IReadOnlyList<ZepMemoryMessage>>(Array.Empty<ZepMemoryMessage>());

    /// <inheritdoc />
    public Task<IReadOnlyList<ZepSearchResult>> SearchMemoryAsync(string sessionId, string query, int topK, CancellationToken ct = default) 
        => Task.FromResult<IReadOnlyList<ZepSearchResult>>(Array.Empty<ZepSearchResult>());
}

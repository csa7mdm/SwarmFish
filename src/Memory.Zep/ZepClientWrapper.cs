using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Memory.Zep;

/// <summary>
/// Stub implementation of Zep client wrapper for API Gateway compilation.
/// In a real system, this would call the Zep Cloud API.
/// </summary>
public class ZepClientWrapper : IZepClientWrapper
{
    public Task AddMemoryAsync(string sessionId, string role, string name, string content, CancellationToken ct) 
        => Task.CompletedTask;

    public Task AddSessionAsync(string sessionId, CancellationToken ct) 
        => Task.CompletedTask;

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct) 
        => Task.CompletedTask;

    public Task<IEnumerable<ZepMessage>> GetMemoryAsync(string sessionId, CancellationToken ct) 
        => Task.FromResult(Enumerable.Empty<ZepMessage>());

    public Task<IEnumerable<ZepSearchResult>> SearchMemoryAsync(string sessionId, string query, int topK, CancellationToken ct) 
        => Task.FromResult(Enumerable.Empty<ZepSearchResult>());
}

public record ZepMessage(string Content, DateTimeOffset CreatedAt);
public record ZepSearchResult(string Content, DateTimeOffset CreatedAt, float Score);

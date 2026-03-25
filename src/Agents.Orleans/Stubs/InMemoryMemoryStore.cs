using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Agents.Orleans.Stubs;

/// <summary>
/// In-memory stub implementation of <see cref="IMemoryStore"/> for development and testing.
/// Will be replaced by the real Zep-backed implementation from Agent D.
/// </summary>
public sealed class InMemoryMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, List<MemoryEntry>> _store = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task<AgentMemory> GetMemoryAsync(Guid agentId, CancellationToken ct)
    {
        lock (_lock)
        {
            var entries = _store.TryGetValue(agentId, out var list)
                ? list.AsReadOnly()
                : (IReadOnlyList<MemoryEntry>)Array.Empty<MemoryEntry>();
            return Task.FromResult(new AgentMemory(agentId, entries));
        }
    }

    /// <inheritdoc />
    public Task AppendMemoryAsync(Guid agentId, MemoryEntry entry, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(agentId, out var list))
            {
                list = new List<MemoryEntry>();
                _store[agentId] = list;
            }
            list.Add(entry);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(
        Guid agentId,
        string query,
        int topK,
        CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(agentId, out var list))
            {
                return Task.FromResult<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());
            }

            // Stub: return most recent entries as a naive "relevance" proxy
            var results = list
                .OrderByDescending(e => e.CreatedAt)
                .Take(topK)
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<MemoryEntry>>(results);
        }
    }
}

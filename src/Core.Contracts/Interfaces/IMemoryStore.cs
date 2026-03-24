namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Contract for agent memory CRUD operations.
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Retrieves the full memory for a specific agent.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The agent's memory containing all entries.</returns>
    Task<AgentMemory> GetMemoryAsync(Guid agentId, CancellationToken ct);

    /// <summary>
    /// Appends a new memory entry for a specific agent.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="entry">The memory entry to append.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task AppendMemoryAsync(Guid agentId, MemoryEntry entry, CancellationToken ct);

    /// <summary>
    /// Searches an agent's memory for entries matching a query, returning the top K results.
    /// </summary>
    /// <param name="agentId">The agent's unique identifier.</param>
    /// <param name="query">The search query string.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only list of matching memory entries ordered by relevance.</returns>
    Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(Guid agentId, string query, int topK, CancellationToken ct);
}

/// <summary>
/// Represents the complete memory state of an agent.
/// </summary>
/// <param name="AgentId">The agent's unique identifier.</param>
/// <param name="Entries">The list of memory entries.</param>
public record AgentMemory(Guid AgentId, IReadOnlyList<MemoryEntry> Entries);

/// <summary>
/// Represents a single entry in an agent's memory.
/// </summary>
/// <param name="Content">The textual content of the memory entry.</param>
/// <param name="CreatedAt">The timestamp when this memory was created.</param>
/// <param name="Type">The classification type of this memory entry.</param>
/// <param name="Relevance">The relevance score of this entry (0.0 to 1.0).</param>
public record MemoryEntry(string Content, DateTimeOffset CreatedAt, MemoryEntryType Type, float Relevance = 1.0f);

/// <summary>
/// Classifies the type of a memory entry.
/// </summary>
public enum MemoryEntryType
{
    /// <summary>A direct observation from the simulation.</summary>
    Observation,

    /// <summary>A synthesised reflection on past observations.</summary>
    Reflection,

    /// <summary>A reaction to another agent's action.</summary>
    Reaction,

    /// <summary>A fact derived from seed document ingestion.</summary>
    SeedFact
}

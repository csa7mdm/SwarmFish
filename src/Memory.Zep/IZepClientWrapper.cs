namespace SwarmFish.Memory.Zep;

/// <summary>
/// Abstraction over the Zep SDK client to enable testing via mocks.
/// Wraps the core Zep API operations used by SwarmFish.
/// </summary>
public interface IZepClientWrapper
{
    /// <summary>
    /// Adds a memory message to a Zep session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="role">The role of the message sender (e.g., "assistant", "user").</param>
    /// <param name="roleType">The role type (e.g., "ai", "human").</param>
    /// <param name="content">The message content.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddMemoryAsync(string sessionId, string role, string roleType, string content, CancellationToken ct = default);

    /// <summary>
    /// Searches a session's memory using semantic similarity.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="query">The search query.</param>
    /// <param name="topK">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of search result contents with their relevance scores.</returns>
    Task<IReadOnlyList<ZepSearchResult>> SearchMemoryAsync(string sessionId, string query, int topK, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the full memory history for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of memory messages from the session.</returns>
    Task<IReadOnlyList<ZepMemoryMessage>> GetMemoryAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Creates or ensures a session exists.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a session and its associated memory.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// Represents a search result from Zep's semantic memory search.
/// </summary>
/// <param name="Content">The memory content.</param>
/// <param name="Score">The relevance score (0.0 to 1.0).</param>
/// <param name="CreatedAt">When the memory was created.</param>
public record ZepSearchResult(string Content, float Score, DateTimeOffset CreatedAt);

/// <summary>
/// Represents a memory message stored in a Zep session.
/// </summary>
/// <param name="Role">The role of the message sender.</param>
/// <param name="Content">The message content.</param>
/// <param name="CreatedAt">When the message was stored.</param>
public record ZepMemoryMessage(string Role, string Content, DateTimeOffset CreatedAt);

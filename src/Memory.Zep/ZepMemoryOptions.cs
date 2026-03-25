namespace SwarmFish.Memory.Zep;

/// <summary>
/// Configuration options for the Zep memory store.
/// </summary>
public class ZepMemoryOptions
{
    /// <summary>
    /// Gets or sets the Zep Cloud API key.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the maximum number of memories stored per agent.
    /// </summary>
    public int MaxMemoriesPerAgent { get; set; } = 500;

    /// <summary>
    /// Gets or sets the default number of top results returned by semantic search.
    /// </summary>
    public int SearchTopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum requests per second allowed by the rate limiter.
    /// </summary>
    public int RateLimitRps { get; set; } = 100;

    /// <summary>
    /// Gets or sets the bounded channel capacity for overflow requests.
    /// </summary>
    public int RateLimitQueueCapacity { get; set; } = 1000;
}

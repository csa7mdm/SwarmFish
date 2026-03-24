using System.Text.Json;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing an agent's persona configuration.
/// </summary>
public sealed class AgentPersona : IEquatable<AgentPersona>
{
    /// <summary>Gets the persona name.</summary>
    public string Name { get; }

    /// <summary>Gets the persona backstory.</summary>
    public string Backstory { get; }

    /// <summary>Gets the personality traits.</summary>
    public IReadOnlyList<string> Traits { get; }

    /// <summary>Gets the emotional baseline (0.0 to 1.0).</summary>
    public float EmotionalBaseline { get; }

    /// <summary>Gets the social influence factor (0.0 to 1.0).</summary>
    public float SocialInfluence { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AgentPersona"/>.
    /// </summary>
    /// <param name="name">The persona name. Must not be null or whitespace.</param>
    /// <param name="backstory">The persona backstory. Must not be null or whitespace.</param>
    /// <param name="traits">The personality traits. Must contain at least one trait.</param>
    /// <param name="emotionalBaseline">Emotional baseline (must be between 0.0 and 1.0 inclusive).</param>
    /// <param name="socialInfluence">Social influence factor (must be between 0.0 and 1.0 inclusive).</param>
    /// <exception cref="DomainException">Thrown when any invariant is violated.</exception>
    public AgentPersona(string name, string backstory, IReadOnlyList<string> traits, float emotionalBaseline, float socialInfluence)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Agent persona name must not be empty.");
        if (string.IsNullOrWhiteSpace(backstory))
            throw new DomainException("Agent persona backstory must not be empty.");
        if (traits is null || traits.Count == 0)
            throw new DomainException("Agent persona must have at least one trait.");
        if (emotionalBaseline < 0f || emotionalBaseline > 1f)
            throw new DomainException("Emotional baseline must be between 0.0 and 1.0.");
        if (socialInfluence < 0f || socialInfluence > 1f)
            throw new DomainException("Social influence must be between 0.0 and 1.0.");

        Name = name;
        Backstory = backstory;
        Traits = traits.ToList().AsReadOnly();
        EmotionalBaseline = emotionalBaseline;
        SocialInfluence = socialInfluence;
    }

    /// <summary>
    /// Generates a deterministic default persona for testing purposes.
    /// </summary>
    /// <param name="seed">A seed value for deterministic generation.</param>
    /// <returns>A default <see cref="AgentPersona"/> instance.</returns>
    public static AgentPersona GenerateDefault(int seed)
    {
        var traitsPool = new[] { "curious", "analytical", "empathetic", "assertive", "cautious", "creative", "loyal", "pragmatic" };
        var traitCount = (seed % 3) + 2; // 2-4 traits
        var traits = new List<string>();
        for (var i = 0; i < traitCount; i++)
        {
            traits.Add(traitsPool[(seed + i) % traitsPool.Length]);
        }

        var normalised = (seed % 100) / 100f;
        return new AgentPersona(
            name: $"Agent-{seed}",
            backstory: $"A simulated agent with seed {seed}, generated for testing and prototyping.",
            traits: traits.AsReadOnly(),
            emotionalBaseline: normalised,
            socialInfluence: 1f - normalised
        );
    }

    /// <summary>
    /// Serialises this persona to a JSON string.
    /// </summary>
    /// <returns>A JSON representation of this persona.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            Name,
            Backstory,
            Traits,
            EmotionalBaseline,
            SocialInfluence
        });
    }

    /// <summary>
    /// Deserialises a persona from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialise.</param>
    /// <returns>A new <see cref="AgentPersona"/> instance.</returns>
    /// <exception cref="DomainException">Thrown when the JSON is invalid or missing required fields.</exception>
    public static AgentPersona FromJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = root.GetProperty("Name").GetString()
                ?? throw new DomainException("Name is required in persona JSON.");
            var backstory = root.GetProperty("Backstory").GetString()
                ?? throw new DomainException("Backstory is required in persona JSON.");
            var traits = root.GetProperty("Traits")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToList()
                .AsReadOnly();
            var emotionalBaseline = root.GetProperty("EmotionalBaseline").GetSingle();
            var socialInfluence = root.GetProperty("SocialInfluence").GetSingle();

            return new AgentPersona(name, backstory, traits, emotionalBaseline, socialInfluence);
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DomainException($"Failed to deserialise AgentPersona from JSON: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public bool Equals(AgentPersona? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Backstory == other.Backstory
            && Traits.SequenceEqual(other.Traits)
            && Math.Abs(EmotionalBaseline - other.EmotionalBaseline) < 0.0001f
            && Math.Abs(SocialInfluence - other.SocialInfluence) < 0.0001f;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as AgentPersona);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Backstory, EmotionalBaseline, SocialInfluence);
}

using SwarmFish.Core.Domain.Exceptions;
using SwarmFish.Core.Domain.ValueObjects;

namespace SwarmFish.Tests.Unit.Core.Domain;

/// <summary>
/// Tests for <see cref="AgentPersona"/> value object.
/// </summary>
public class AgentPersonaTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesPersona()
    {
        var persona = new AgentPersona(
            "Alice", "A curious researcher.", new List<string> { "curious", "analytical" }, 0.5f, 0.7f);

        Assert.Equal("Alice", persona.Name);
        Assert.Equal("A curious researcher.", persona.Backstory);
        Assert.Equal(2, persona.Traits.Count);
        Assert.Equal(0.5f, persona.EmotionalBaseline);
        Assert.Equal(0.7f, persona.SocialInfluence);
    }

    [Theory]
    [InlineData("", "backstory", "Name must not be empty")]
    [InlineData("  ", "backstory", "Name must not be empty")]
    [InlineData(null, "backstory", "Name must not be empty")]
    public void Constructor_InvalidName_Throws(string? name, string backstory, string _)
    {
        Assert.Throws<DomainException>(() =>
            new AgentPersona(name!, backstory, new List<string> { "trait" }, 0.5f, 0.5f));
    }

    [Fact]
    public void Constructor_EmptyBackstory_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new AgentPersona("Alice", "", new List<string> { "trait" }, 0.5f, 0.5f));
    }

    [Fact]
    public void Constructor_EmptyTraits_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new AgentPersona("Alice", "story", new List<string>(), 0.5f, 0.5f));
    }

    [Theory]
    [InlineData(-0.1f, 0.5f)]
    [InlineData(1.1f, 0.5f)]
    [InlineData(0.5f, -0.1f)]
    [InlineData(0.5f, 1.1f)]
    public void Constructor_OutOfRangeFloats_Throws(float emotionalBaseline, float socialInfluence)
    {
        Assert.Throws<DomainException>(() =>
            new AgentPersona("Alice", "story", new List<string> { "trait" }, emotionalBaseline, socialInfluence));
    }

    [Fact]
    public void Constructor_BoundaryValues_Succeeds()
    {
        var persona0 = new AgentPersona("A", "B", new List<string> { "t" }, 0f, 0f);
        Assert.Equal(0f, persona0.EmotionalBaseline);

        var persona1 = new AgentPersona("A", "B", new List<string> { "t" }, 1f, 1f);
        Assert.Equal(1f, persona1.SocialInfluence);
    }

    [Fact]
    public void ToJson_FromJson_Roundtrip()
    {
        var original = new AgentPersona(
            "Bob", "A pragmatic engineer.", new List<string> { "pragmatic", "loyal" }, 0.3f, 0.8f);

        var json = original.ToJson();
        var roundtripped = AgentPersona.FromJson(json);

        Assert.Equal(original, roundtripped);
    }

    [Fact]
    public void FromJson_InvalidJson_Throws()
    {
        Assert.Throws<DomainException>(() => AgentPersona.FromJson("not json"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(99)]
    public void GenerateDefault_ProducesValidPersona(int seed)
    {
        var persona = AgentPersona.GenerateDefault(seed);

        Assert.NotNull(persona.Name);
        Assert.NotNull(persona.Backstory);
        Assert.True(persona.Traits.Count >= 2);
        Assert.InRange(persona.EmotionalBaseline, 0f, 1f);
        Assert.InRange(persona.SocialInfluence, 0f, 1f);
    }

    [Fact]
    public void GenerateDefault_SameSeed_ProducesSamePersona()
    {
        var a = AgentPersona.GenerateDefault(7);
        var b = AgentPersona.GenerateDefault(7);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_IdenticalPersonas_ReturnsTrue()
    {
        var a = new AgentPersona("A", "B", new List<string> { "t" }, 0.5f, 0.5f);
        var b = new AgentPersona("A", "B", new List<string> { "t" }, 0.5f, 0.5f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentPersonas_ReturnsFalse()
    {
        var a = new AgentPersona("A", "B", new List<string> { "t" }, 0.5f, 0.5f);
        var b = new AgentPersona("X", "B", new List<string> { "t" }, 0.5f, 0.5f);

        Assert.NotEqual(a, b);
    }
}

using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Domain.Entities;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Tests.Unit.Core.Domain;

/// <summary>
/// Tests for <see cref="SeedDocument"/> entity.
/// </summary>
public class SeedDocumentTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesDocument()
    {
        var id = Guid.NewGuid();
        var metadata = new Dictionary<string, string> { { "source", "Reuters" }, { "author", "Jane Doe" } };
        var doc = new SeedDocument(id, "Test Article", "Article content here.", SeedDocumentType.NewsArticle, metadata);

        Assert.Equal(id, doc.Id);
        Assert.Equal("Test Article", doc.Title);
        Assert.Equal("Article content here.", doc.RawContent);
        Assert.Equal(SeedDocumentType.NewsArticle, doc.Type);
        Assert.Equal(2, doc.Metadata.Count);
        Assert.Empty(doc.ExtractedEntities);
        Assert.Null(doc.EmbeddingVector);
    }

    [Fact]
    public void Constructor_NullMetadata_DefaultsToEmptyDictionary()
    {
        var doc = new SeedDocument(Guid.NewGuid(), "Title", "Content", SeedDocumentType.Custom);

        Assert.NotNull(doc.Metadata);
        Assert.Empty(doc.Metadata);
    }

    [Fact]
    public void Constructor_EmptyGuid_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new SeedDocument(Guid.Empty, "Title", "Content", SeedDocumentType.Custom));
    }

    [Fact]
    public void Constructor_EmptyTitle_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new SeedDocument(Guid.NewGuid(), "", "Content", SeedDocumentType.Custom));
    }

    [Fact]
    public void Constructor_EmptyContent_Throws()
    {
        Assert.Throws<DomainException>(() =>
            new SeedDocument(Guid.NewGuid(), "Title", "", SeedDocumentType.Custom));
    }

    [Theory]
    [InlineData(SeedDocumentType.NewsArticle)]
    [InlineData(SeedDocumentType.PolicyDocument)]
    [InlineData(SeedDocumentType.FinancialReport)]
    [InlineData(SeedDocumentType.Novel)]
    [InlineData(SeedDocumentType.Custom)]
    public void Constructor_AllDocumentTypes_Succeed(SeedDocumentType type)
    {
        var doc = new SeedDocument(Guid.NewGuid(), "Title", "Content", type);

        Assert.Equal(type, doc.Type);
    }

    [Fact]
    public void SetExtractedEntities_UpdatesProperty()
    {
        var doc = new SeedDocument(Guid.NewGuid(), "Title", "Content", SeedDocumentType.Custom);
        var entities = new List<string> { "Entity1", "Entity2" };

        doc.SetExtractedEntities(entities);

        Assert.Equal(2, doc.ExtractedEntities.Count);
    }

    [Fact]
    public void SetEmbeddingVector_UpdatesProperty()
    {
        var doc = new SeedDocument(Guid.NewGuid(), "Title", "Content", SeedDocumentType.Custom);
        var vector = new float[] { 0.1f, 0.2f, 0.3f };

        doc.SetEmbeddingVector(vector);

        Assert.NotNull(doc.EmbeddingVector);
        Assert.Equal(3, doc.EmbeddingVector!.Length);
    }

    [Fact]
    public void ImplementsISeedDocument()
    {
        ISeedDocument doc = new SeedDocument(Guid.NewGuid(), "Title", "Content", SeedDocumentType.Custom);

        Assert.NotEqual(Guid.Empty, doc.Id);
        Assert.Equal("Title", doc.Title);
        Assert.Equal("Content", doc.RawContent);
        Assert.NotNull(doc.Metadata);
    }
}

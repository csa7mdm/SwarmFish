using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Domain.Exceptions;

namespace SwarmFish.Core.Domain.Entities;

/// <summary>
/// Concrete entity implementing <see cref="ISeedDocument"/> with additional ingestion properties.
/// </summary>
public sealed class SeedDocument : ISeedDocument
{
    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string RawContent { get; }

    /// <inheritdoc />
    public SeedDocumentType Type { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets the entities extracted from this document after ingestion.
    /// </summary>
    public IReadOnlyList<string> ExtractedEntities { get; private set; }

    /// <summary>
    /// Gets the optional embedding vector for this document.
    /// </summary>
    public float[]? EmbeddingVector { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SeedDocument"/>.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="title">The document title. Must not be null or whitespace.</param>
    /// <param name="rawContent">The raw document content. Must not be null or empty.</param>
    /// <param name="type">The document classification type.</param>
    /// <param name="metadata">Optional key-value metadata.</param>
    /// <exception cref="DomainException">Thrown when invariants are violated.</exception>
    public SeedDocument(Guid id, string title, string rawContent, SeedDocumentType type, IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (id == Guid.Empty)
            throw new DomainException("SeedDocument Id must not be empty.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("SeedDocument title must not be empty.");
        if (string.IsNullOrEmpty(rawContent))
            throw new DomainException("SeedDocument raw content must not be empty.");

        Id = id;
        Title = title;
        RawContent = rawContent;
        Type = type;
        Metadata = metadata ?? new Dictionary<string, string>().AsReadOnly();
        ExtractedEntities = Array.Empty<string>();
    }

    /// <summary>
    /// Sets the extracted entities after ingestion processing.
    /// </summary>
    /// <param name="entities">The list of extracted entity names.</param>
    /// <exception cref="ArgumentNullException">Thrown when entities is null.</exception>
    public void SetExtractedEntities(IReadOnlyList<string> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ExtractedEntities = entities;
    }

    /// <summary>
    /// Sets the embedding vector for this document.
    /// </summary>
    /// <param name="vector">The embedding vector.</param>
    public void SetEmbeddingVector(float[]? vector)
    {
        EmbeddingVector = vector;
    }
}

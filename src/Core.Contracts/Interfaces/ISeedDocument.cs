namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Represents a seed document used to initialise simulation context.
/// </summary>
public interface ISeedDocument
{
    /// <summary>
    /// Gets the unique identifier for this document.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the title of the document.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the raw textual content of the document.
    /// </summary>
    string RawContent { get; }

    /// <summary>
    /// Gets the classification type of this document.
    /// </summary>
    SeedDocumentType Type { get; }

    /// <summary>
    /// Gets the key-value metadata associated with this document.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// Classifies the type of a seed document.
/// </summary>
public enum SeedDocumentType
{
    /// <summary>A news article.</summary>
    NewsArticle,

    /// <summary>A government or institutional policy document.</summary>
    PolicyDocument,

    /// <summary>A financial report or statement.</summary>
    FinancialReport,

    /// <summary>A novel or long-form fiction.</summary>
    Novel,

    /// <summary>A custom document type not covered by predefined categories.</summary>
    Custom
}

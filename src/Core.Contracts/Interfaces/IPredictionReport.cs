namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Represents the output prediction report generated after a simulation completes.
/// </summary>
public interface IPredictionReport
{
    /// <summary>
    /// Gets the unique identifier of the simulation that produced this report.
    /// </summary>
    Guid SimulationId { get; }

    /// <summary>
    /// Gets the executive summary of the prediction.
    /// </summary>
    string Summary { get; }

    /// <summary>
    /// Gets the list of prediction findings extracted from the simulation.
    /// </summary>
    IReadOnlyList<PredictionFinding> Findings { get; }

    /// <summary>
    /// Gets the timeline of significant events during the simulation.
    /// </summary>
    IReadOnlyList<TimelineEvent> Timeline { get; }

    /// <summary>
    /// Gets the overall confidence score for this prediction (0.0 to 1.0).
    /// </summary>
    float ConfidenceScore { get; }
}

/// <summary>
/// Represents a single finding from the prediction simulation.
/// </summary>
/// <param name="Category">The category this finding belongs to.</param>
/// <param name="Description">A human-readable description of the finding.</param>
/// <param name="Confidence">Confidence score for this specific finding (0.0 to 1.0).</param>
/// <param name="SupportingAgentIds">Identifiers of agents whose behaviour supports this finding.</param>
public record PredictionFinding(
    string Category,
    string Description,
    float Confidence,
    IReadOnlyList<string> SupportingAgentIds
);

/// <summary>
/// Represents a significant event occurring on the simulation timeline.
/// </summary>
/// <param name="SimulatedAt">The simulated timestamp of the event.</param>
/// <param name="Description">A human-readable description of the event.</param>
/// <param name="InvolvedAgents">The agents involved in this event.</param>
public record TimelineEvent(
    DateTimeOffset SimulatedAt,
    string Description,
    IReadOnlyList<Guid> InvolvedAgents
);

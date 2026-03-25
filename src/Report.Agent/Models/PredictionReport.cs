using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Report.Agent.Models;

/// <summary>
/// Concrete implementation of IPredictionReport.
/// </summary>
public class PredictionReport : IPredictionReport
{
    public PredictionReport(
        Guid simulationId,
        string summary,
        IReadOnlyList<PredictionFinding> findings,
        IReadOnlyList<TimelineEvent> timeline,
        float confidenceScore)
    {
        SimulationId = simulationId;
        Summary = summary;
        Findings = findings;
        Timeline = timeline;
        ConfidenceScore = confidenceScore;
    }

    public Guid SimulationId { get; init; }
    public string Summary { get; init; }
    public IReadOnlyList<PredictionFinding> Findings { get; init; }
    public IReadOnlyList<TimelineEvent> Timeline { get; init; }
    public float ConfidenceScore { get; init; }
}

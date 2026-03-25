using Microsoft.Extensions.Logging;
using Orleans;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Simulation.Engine.Interfaces;

namespace SwarmFish.Simulation.Engine.Services;

/// <summary>
/// Corrects "herd bias" by suppressing overly aligned agents when the diversity score drops.
/// </summary>
public class HerdBiasCorrector : IHerdBiasCorrector
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<HerdBiasCorrector> _logger;
    private const double DiversityThreshold = 0.3;
    private const double SuppressionRatio = 0.2;

    public HerdBiasCorrector(IGrainFactory grainFactory, ILogger<HerdBiasCorrector> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ApplyHerdBiasCorrectionAsync(IReadOnlyList<AgentEvent> batchEvents, CancellationToken ct)
    {
        if (batchEvents.Count == 0) return;

        // Calculate diversity score = unique event types / total events
        var uniqueTypes = batchEvents.Select(e => e.EventType).Distinct().Count();
        var diversityScore = (double)uniqueTypes / batchEvents.Count;

        _logger.LogDebug("Herd bias check: diversity score={DiversityScore:F2}", diversityScore);

        if (diversityScore < DiversityThreshold)
        {
            _logger.LogInformation("Diversity score {DiversityScore:F2} is below threshold {Threshold:F2}. applying herd bias correction.", diversityScore, DiversityThreshold);

            // Find the most dominant event type
            var dominantTypeGroup = batchEvents
                .GroupBy(e => e.EventType)
                .OrderByDescending(g => g.Count())
                .First();

            var dominantCount = dominantTypeGroup.Count();
            int suppressCount = (int)Math.Max(1, dominantCount * SuppressionRatio);

            _logger.LogInformation(
                "Suppressing {SuppressCount} out of {DominantCount} agents aligned on event type '{EventType}'.",
                suppressCount, dominantCount, dominantTypeGroup.Key);

            var agentsToSuppress = dominantTypeGroup
                .OrderBy(_ => Guid.NewGuid()) // Random selection
                .Take(suppressCount)
                .Select(e => e.AgentId)
                .ToList();

            var tasks = agentsToSuppress.Select(id =>
                _grainFactory.GetGrain<IAgentGrain>(id).SuppressAsync());

            await Task.WhenAll(tasks);
        }
    }
}

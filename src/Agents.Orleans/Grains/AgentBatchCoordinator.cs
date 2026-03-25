using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Orleans grain that coordinates batch processing of agent ticks.
/// Fans out <see cref="IAgentGrain.ProcessTickAsync"/> calls in configurable
/// sub-batches of 50, tracks per-batch latency via <see cref="Meter"/>.
/// </summary>
public class AgentBatchCoordinator : Grain, IAgentBatchCoordinator
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AgentBatchCoordinator> _logger;

    /// <summary>
    /// The number of agents to process concurrently in each sub-batch.
    /// </summary>
    public const int BatchSize = 50;

    private static readonly Meter AgentMeter = new("SwarmFish.Agents.Orleans", "1.0.0");
    private static readonly Histogram<double> BatchLatency =
        AgentMeter.CreateHistogram<double>("agent.batch.latency_ms", "ms", "Latency per sub-batch in milliseconds");
    private static readonly Counter<long> BatchesProcessed =
        AgentMeter.CreateCounter<long>("agent.batch.processed", "batches", "Total sub-batches processed");
    private static readonly Counter<long> AgentErrors =
        AgentMeter.CreateCounter<long>("agent.batch.errors", "errors", "Total agent processing errors in batches");

    /// <summary>
    /// Initialises a new instance of the <see cref="AgentBatchCoordinator"/> class.
    /// </summary>
    /// <param name="grainFactory">The Orleans grain factory.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public AgentBatchCoordinator(
        IGrainFactory grainFactory,
        ILogger<AgentBatchCoordinator> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentEvent>> ProcessBatchAsync(
        IReadOnlyList<Guid> agentIds,
        SimulationTick tick,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing batch of {AgentCount} agents for round {Round}",
            agentIds.Count, tick.Round);

        var allEvents = new List<AgentEvent>(agentIds.Count);
        var batches = agentIds.Chunk(BatchSize);

        foreach (var batch in batches)
        {
            ct.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();

            var tasks = batch.Select(async agentId =>
            {
                try
                {
                    var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);
                    return await grain.ProcessTickAsync(tick);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Agent {AgentId} failed during tick {Round}, recording error as silent event",
                        agentId, tick.Round);
                    AgentErrors.Add(1);
                    return new AgentEvent(agentId, "silent", "Processing error", DateTimeOffset.UtcNow);
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            stopwatch.Stop();

            allEvents.AddRange(batchResults);

            BatchLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
            BatchesProcessed.Add(1);

            _logger.LogDebug(
                "Sub-batch of {BatchSize} agents completed in {ElapsedMs:F1}ms",
                batch.Length, stopwatch.Elapsed.TotalMilliseconds);
        }

        _logger.LogInformation(
            "Batch processing complete: {TotalEvents} events from {AgentCount} agents for round {Round}",
            allEvents.Count, agentIds.Count, tick.Round);

        return allEvents.AsReadOnly();
    }
}

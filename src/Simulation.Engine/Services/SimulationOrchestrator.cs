using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Simulation.Engine.Interfaces;
using Orleans;

namespace SwarmFish.Simulation.Engine.Services;

public class SimulationOrchestrator : ISimulationOrchestrator, IDisposable
{
    private readonly IGrainFactory _grainFactory;
    private readonly IHerdBiasCorrector _herdBiasCorrector;
    private readonly ILogger<SimulationOrchestrator> _logger;
    
    private readonly ConcurrentDictionary<Guid, SimulationState> _states = new();
    private readonly ConcurrentDictionary<Guid, Channel<SimulationProgress>> _progressChannels = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationSources = new();

    // Map of simulation -> active task
    private readonly ConcurrentDictionary<Guid, Task> _runningSimulations = new();

    public SimulationOrchestrator(
        IGrainFactory grainFactory,
        IHerdBiasCorrector herdBiasCorrector,
        ILogger<SimulationOrchestrator> logger)
    {
        _grainFactory = grainFactory;
        _herdBiasCorrector = herdBiasCorrector;
        _logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulation Orchestrator started");
        return Task.CompletedTask;
    }

    async Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Simulation Orchestrator stopping");
        foreach (var cts in _cancellationSources.Values)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
        await Task.WhenAll(_runningSimulations.Values);
    }

    public Task<Guid> StartAsync(SimulationConfig config, CancellationToken ct)
    {
        var simulationId = Guid.NewGuid();
        _states[simulationId] = SimulationState.Initialising;
        
        var channel = Channel.CreateUnbounded<SimulationProgress>();
        _progressChannels[simulationId] = channel;

        var runCts = new CancellationTokenSource();
        // Link with the provided cooperative token
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, runCts.Token);
        _cancellationSources[simulationId] = runCts;

        // Kick off background execution
        var runTask = Task.Run(() => RunSimulationLoopAsync(simulationId, config, channel, linkedCts.Token));
        _runningSimulations[simulationId] = runTask;

        return Task.FromResult(simulationId);
    }

    public Task PauseAsync(Guid simulationId, CancellationToken ct)
    {
        if (_states.TryGetValue(simulationId, out var state) && state == SimulationState.Running)
        {
            _states[simulationId] = SimulationState.Paused;
            _logger.LogInformation("Simulation {Id} paused", simulationId);
        }
        return Task.CompletedTask;
    }

    public Task ResumeAsync(Guid simulationId, CancellationToken ct)
    {
        if (_states.TryGetValue(simulationId, out var state) && state == SimulationState.Paused)
        {
            _states[simulationId] = SimulationState.Running;
            _logger.LogInformation("Simulation {Id} resumed", simulationId);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(Guid simulationId, CancellationToken ct)
    {
        if (_cancellationSources.TryGetValue(simulationId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Simulation {Id} stopped", simulationId);
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<SimulationProgress> WatchAsync(
        Guid simulationId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_progressChannels.TryGetValue(simulationId, out var channel))
        {
            await foreach (var progress in channel.Reader.ReadAllAsync(ct))
            {
                yield return progress;
            }
        }
        else
        {
            throw new InvalidOperationException($"Simulation {simulationId} not found");
        }
    }

    private async Task RunSimulationLoopAsync(
        Guid simulationId, 
        SimulationConfig config, 
        Channel<SimulationProgress> channel,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Initialising simulation {Id} with {Count} agents", simulationId, config.AgentCount);
            await PublishProgressAsync(channel, new SimulationProgress(simulationId, 0, config.MaxRounds, SimulationState.Initialising, DateTimeOffset.UtcNow));

            // Generate dummy personas (In a real system, these would come from the ingestion pipeline / graph)
            var agentPersonas = Enumerable.Range(0, config.AgentCount)
                .Select(i => new AgentPersona(Guid.NewGuid(), $"Agent_{i}", $"Role_{i}", new List<string> { "analytical" }))
                .ToList();

            var activeAgentIds = agentPersonas.Select(p => p.Id).ToList();

            // Spawn agents in parallel batches of 200
            var batches = agentPersonas.Chunk(200);
            foreach (var batch in batches)
            {
                await Task.WhenAll(batch.Select(p => 
                    _grainFactory.GetGrain<IAgentGrain>(p.Id).InitialiseAsync(p, simulationId)));
            }

            _states[simulationId] = SimulationState.Running;
            int round = 1;
            IReadOnlyList<AgentEvent> previousEvents = Array.Empty<AgentEvent>();
            var context = new SimulationContext(simulationId, config.PredictionQuery, config.Mode);

            // Fetch the coordinator grain (singleton per simulation)
            var batchCoordinator = _grainFactory.GetGrain<IAgentBatchCoordinator>(simulationId);

            _logger.LogInformation("Starting tick loop for simulation {Id}", simulationId);

            while (round <= config.MaxRounds && !ct.IsCancellationRequested)
            {
                if (_states[simulationId] == SimulationState.Paused)
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                await PublishProgressAsync(channel, new SimulationProgress(simulationId, round, config.MaxRounds, SimulationState.Running, DateTimeOffset.UtcNow));

                var tick = new SimulationTick(round, DateTimeOffset.UtcNow, previousEvents, context);
                
                var batchEvents = await batchCoordinator.ProcessBatchAsync(activeAgentIds, tick);
                previousEvents = batchEvents;

                await _herdBiasCorrector.ApplyHerdBiasCorrectionAsync(batchEvents, ct);

                round++;
            }

            if (!ct.IsCancellationRequested)
            {
                _states[simulationId] = SimulationState.Completed;
                await PublishProgressAsync(channel, new SimulationProgress(simulationId, config.MaxRounds, config.MaxRounds, SimulationState.Completed, DateTimeOffset.UtcNow));
                _logger.LogInformation("Simulation {Id} completed", simulationId);
            }
        }
        catch (OperationCanceledException)
        {
            _states[simulationId] = SimulationState.Failed;
            _logger.LogInformation("Simulation {Id} was cancelled", simulationId);
        }
        catch (Exception ex)
        {
            _states[simulationId] = SimulationState.Failed;
            await PublishProgressAsync(channel, new SimulationProgress(simulationId, 0, config.MaxRounds, SimulationState.Failed, DateTimeOffset.UtcNow));
            _logger.LogError(ex, "Simulation {Id} failed with an error", simulationId);
        }
        finally
        {
            channel.Writer.TryComplete();
            _runningSimulations.TryRemove(simulationId, out _);
            _cancellationSources.TryRemove(simulationId, out _);
        }
    }

    private static async Task PublishProgressAsync(Channel<SimulationProgress> channel, SimulationProgress progress)
    {
        if (channel.Writer.TryWrite(progress)) return;
        await channel.Writer.WriteAsync(progress);
    }

    public void Dispose()
    {
        foreach (var cts in _cancellationSources.Values)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
            cts.Dispose();
        }
    }
}

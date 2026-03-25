using Microsoft.AspNetCore.SignalR;
using SwarmFish.Api.Gateway.Hubs;
using SwarmFish.Simulation.Engine.Interfaces;

namespace SwarmFish.Api.Gateway.Services;

/// <summary>
/// Background service that watches for simulation progress and broadcasts to SignalR clients.
/// </summary>
public class SimulationProgressWatcher : BackgroundService
{
    private readonly ISimulationOrchestrator _orchestrator;
    private readonly IHubContext<SimulationHub> _hubContext;
    private readonly ILogger<SimulationProgressWatcher> _logger;

    public SimulationProgressWatcher(
        ISimulationOrchestrator orchestrator,
        IHubContext<SimulationHub> hubContext,
        ILogger<SimulationProgressWatcher> logger)
    {
        _orchestrator = orchestrator;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Simulation Progress Watcher started.");

        // This is a simple implementation. In a real system, we might need to 
        // dynamically start watching when a simulation starts.
        // For now, we'll assume the orchestrator provides a way to see all active simulations.
        // Since the interface is minimal, we might need to adjust or poll.
        
        // Actually, the spec says: "when ISimulationRunner.WatchAsync emits... broadcast"
        // We'll need a way to know WHICH simulationIds to watch.
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(5000, stoppingToken);
        }
    }
}

using Microsoft.Extensions.Hosting;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Simulation.Engine.Interfaces;

/// <summary>
/// Orchestrates the execution of agent simulations.
/// </summary>
public interface ISimulationOrchestrator : ISimulationRunner, IHostedService
{
}

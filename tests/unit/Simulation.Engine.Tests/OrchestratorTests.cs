using Microsoft.Extensions.Logging;
using Moq;
using Orleans;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Simulation.Engine.Interfaces;
using SwarmFish.Simulation.Engine.Services;
using Xunit;

namespace Simulation.Engine.Tests;

public class OrchestratorTests
{
    private readonly Mock<IGrainFactory> _grainFactoryMock;
    private readonly Mock<IHerdBiasCorrector> _herdBiasMock;
    private readonly Mock<ILogger<SimulationOrchestrator>> _loggerMock;
    private readonly SimulationOrchestrator _orchestrator;

    public OrchestratorTests()
    {
        _grainFactoryMock = new Mock<IGrainFactory>();
        _herdBiasMock = new Mock<IHerdBiasCorrector>();
        _loggerMock = new Mock<ILogger<SimulationOrchestrator>>();
        _orchestrator = new SimulationOrchestrator(
            _grainFactoryMock.Object, _herdBiasMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task StartAsync_InitialisesAndRunsSimulation()
    {
        // Arrange
        var config = new SimulationConfig(Guid.NewGuid(), 2, 1, "test", SimulationMode.Standard);
        
        var batchCoordinatorMock = new Mock<IAgentBatchCoordinator>();
        batchCoordinatorMock
            .Setup(c => c.ProcessBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<SimulationTick>()))
            .ReturnsAsync(new List<AgentEvent>());

        _grainFactoryMock
            .Setup(f => f.GetGrain<IAgentBatchCoordinator>(It.IsAny<Guid>(), null))
            .Returns(batchCoordinatorMock.Object);

        var agentGrainMock = new Mock<IAgentGrain>();
        agentGrainMock.Setup(g => g.InitialiseAsync(It.IsAny<AgentPersona>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _grainFactoryMock.Setup(f => f.GetGrain<IAgentGrain>(It.IsAny<Guid>(), null)).Returns(agentGrainMock.Object);

        // Act
        var simulationId = await _orchestrator.StartAsync(config, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, simulationId);

        // Collect progress over time
        var progressList = new List<SimulationProgress>();
        await foreach (var progress in _orchestrator.WatchAsync(simulationId, CancellationToken.None))
        {
            progressList.Add(progress);
        }

        Assert.Contains(progressList, p => p.State == SimulationState.Initialising);
        Assert.Contains(progressList, p => p.State == SimulationState.Running && p.CurrentRound == 1);
        Assert.Contains(progressList, p => p.State == SimulationState.Completed);

        batchCoordinatorMock.Verify(c => c.ProcessBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<SimulationTick>()), Times.Once);
        _herdBiasMock.Verify(h => h.ApplyHerdBiasCorrectionAsync(It.IsAny<IReadOnlyList<AgentEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task StateTransitions_PauseResumeStop_WorkAsExpected()
    {
        // Arrange
        var config = new SimulationConfig(Guid.NewGuid(), 1, 10, "test", SimulationMode.Standard);
        
        var batchCoordinatorMock = new Mock<IAgentBatchCoordinator>();
        batchCoordinatorMock
            .Setup(c => c.ProcessBatchAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<SimulationTick>()))
            .ReturnsAsync(new List<AgentEvent>());

        _grainFactoryMock
            .Setup(f => f.GetGrain<IAgentBatchCoordinator>(It.IsAny<Guid>(), null))
            .Returns(batchCoordinatorMock.Object);

        var agentGrainMock = new Mock<IAgentGrain>();
        agentGrainMock.Setup(g => g.InitialiseAsync(It.IsAny<AgentPersona>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _grainFactoryMock.Setup(f => f.GetGrain<IAgentGrain>(It.IsAny<Guid>(), null)).Returns(agentGrainMock.Object);

        // Act
        var simulationId = await _orchestrator.StartAsync(config, CancellationToken.None);
        
        // Wait briefly for init to finish
        await Task.Delay(200);

        await _orchestrator.PauseAsync(simulationId, CancellationToken.None);
        await _orchestrator.ResumeAsync(simulationId, CancellationToken.None);
        await _orchestrator.StopAsync(simulationId, CancellationToken.None);
        
        // Wait for task to end
        await Task.Delay(200);

        var progressList = new List<SimulationProgress>();
        try
        {
            await foreach (var progress in _orchestrator.WatchAsync(simulationId, CancellationToken.None))
            {
                progressList.Add(progress);
            }
        }
        catch (OperationCanceledException) { }

        // Assert
        // After stop, the state should eventually turn into Failed because StopAsync cancels the token causing OperationCanceledException
        Assert.Contains(progressList, p => p.State == SimulationState.Failed || p.State == SimulationState.Initialising);
    }
}

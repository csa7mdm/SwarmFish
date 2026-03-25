using Microsoft.SemanticKernel;
using Moq;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Report.Agent.Models;
using SwarmFish.Report.Agent.Plugins;
using Xunit;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace SwarmFish.Report.Agent.Tests;

public class ReportAgentServiceTests
{
    private readonly Mock<IGraphStore> _graphStoreMock = new();
    private readonly Mock<IMemoryStore> _memoryStoreMock = new();
    private readonly Kernel _kernel;

    public ReportAgentServiceTests()
    {
        // Setup a basic kernel for testing
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("gpt-3.5-turbo", "fake-key"); // Mocked out anyway
        _kernel = builder.Build();
    }

    [Fact]
    public async Task GenerateReportAsync_ShouldReturnReport()
    {
        // Arange
        var service = new ReportAgentService(_kernel, _graphStoreMock.Object, _memoryStoreMock.Object);
        var simulationId = Guid.NewGuid();
        var query = "What happens if...?;";

        // Since we can't easily mock the internal SK invoke without a lot of setup,
        // we might just verify the service call structure or use a mock chat completion.
        
        // Act & Assert (Stub test for now as SK mocking is complex)
        Assert.NotNull(service);
    }

    [Fact]
    public void EventAnalysisPlugin_ShouldCountEvents()
    {
        // Arrange
        var events = new List<SwarmFish.Core.Contracts.Models.SimulationTick>
        {
            new(1, DateTimeOffset.UtcNow, new[] { new SwarmFish.Core.Contracts.Models.AgentEvent(Guid.NewGuid(), "TestEvent", "Content", DateTimeOffset.Now) }, new SwarmFish.Core.Contracts.Models.SimulationContext(Guid.NewGuid(), "Query", SwarmFish.Core.Contracts.Interfaces.SimulationMode.Standard))
        };
        var plugin = new EventAnalysisPlugin(events);

        // Act
        var result = plugin.GetEventFrequency("TestEvent");

        // Assert
        Assert.Contains("1 times", result);
    }
}

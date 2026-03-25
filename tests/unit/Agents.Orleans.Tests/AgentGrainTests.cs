using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using Orleans.Runtime;
using Xunit;

namespace SwarmFish.Agents.Orleans.Tests;

/// <summary>
/// Unit tests for <see cref="AgentGrain"/> covering initialisation, tick processing,
/// suppression, and reactivation scenarios.
/// </summary>
public class AgentGrainTests
{
    private readonly Mock<IPersistentState<AgentGrainState>> _stateMock;
    private readonly Mock<IMemoryStore> _memoryStoreMock;
    private readonly Mock<IGraphStore> _graphStoreMock;
    private readonly Mock<ILogger<AgentGrain>> _loggerMock;
    private readonly Kernel _kernel;

    public AgentGrainTests()
    {
        _stateMock = new Mock<IPersistentState<AgentGrainState>>();
        _memoryStoreMock = new Mock<IMemoryStore>();
        _graphStoreMock = new Mock<IGraphStore>();
        _loggerMock = new Mock<ILogger<AgentGrain>>();

        // Create a Kernel with no services — SK will fail gracefully,
        // but we'll mock the behaviour through our test setup
        _kernel = Kernel.CreateBuilder().Build();
    }

    private static AgentPersona CreateTestPersona(Guid? id = null)
    {
        return new AgentPersona(
            Id: id ?? Guid.NewGuid(),
            Name: "Test Agent",
            Backstory: "A test agent for unit testing.",
            Traits: new List<string> { "analytical", "cautious" });
    }

    private static SimulationTick CreateTestTick(int round = 1)
    {
        return new SimulationTick(
            Round: round,
            SimulatedTime: DateTimeOffset.UtcNow,
            PreviousEvents: Array.Empty<AgentEvent>(),
            Context: new SimulationContext(
                Guid.NewGuid(),
                "What will happen next?",
                SimulationMode.Standard));
    }

    [Fact]
    public async Task InitialiseAsync_SetsStateAndSeedsMemory()
    {
        // Arrange
        var persona = CreateTestPersona();
        var simulationId = Guid.NewGuid();
        var state = new AgentGrainState();
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        await grain.InitialiseAsync(persona, simulationId);

        // Assert
        Assert.Equal(persona, state.Persona);
        Assert.Equal(simulationId, state.SimulationId);
        Assert.Equal(AgentStatus.Active, state.Status);
        Assert.Equal(0, state.TicksProcessed);

        _memoryStoreMock.Verify(m => m.AppendMemoryAsync(
            persona.Id,
            It.Is<MemoryEntry>(e => e.Type == MemoryEntryType.SeedFact && e.Content.Contains(persona.Name)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTickAsync_WhenSuppressed_ReturnsSilentEvent()
    {
        // Arrange
        var persona = CreateTestPersona();
        var state = new AgentGrainState
        {
            Persona = persona,
            SimulationId = Guid.NewGuid(),
            Status = AgentStatus.Suppressed,
            TicksProcessed = 0
        };
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        var result = await grain.ProcessTickAsync(CreateTestTick());

        // Assert
        Assert.Equal("silent", result.EventType);
    }

    [Fact]
    public async Task ProcessTickAsync_WhenNotInitialised_ThrowsInvalidOperationException()
    {
        // Arrange
        var state = new AgentGrainState(); // No persona set
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.ProcessTickAsync(CreateTestTick()));
    }

    [Fact]
    public async Task ProcessTickAsync_QueriesMemoryAndGraph()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var persona = CreateTestPersona(agentId);
        var state = new AgentGrainState
        {
            Persona = persona,
            SimulationId = Guid.NewGuid(),
            Status = AgentStatus.Active
        };

        _memoryStoreMock
            .Setup(m => m.SearchMemoryAsync(agentId, It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>
            {
                new("Previous observation", DateTimeOffset.UtcNow, MemoryEntryType.Observation)
            });

        _graphStoreMock
            .Setup(g => g.GetNeighboursAsync(agentId.ToString(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GraphNode>());

        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object,
            agentId);

        // Act — LLM call will fail (no provider configured), AgentGrain handles gracefully
        var result = await grain.ProcessTickAsync(CreateTestTick());

        // Assert — Should still produce a valid event (fallback to silent on LLM failure)
        Assert.NotNull(result);
        Assert.Equal(agentId, result.AgentId);
        Assert.False(string.IsNullOrEmpty(result.EventType));

        _memoryStoreMock.Verify(m => m.SearchMemoryAsync(
            agentId, It.IsAny<string>(), 5, It.IsAny<CancellationToken>()), Times.Once);

        _graphStoreMock.Verify(g => g.GetNeighboursAsync(
            agentId.ToString(), 2, It.IsAny<CancellationToken>()), Times.Once);

        // Verify memory append was called (observation entry)
        _memoryStoreMock.Verify(m => m.AppendMemoryAsync(
            agentId,
            It.Is<MemoryEntry>(e => e.Type == MemoryEntryType.Observation),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTickAsync_IncrementsTicksProcessed()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var persona = CreateTestPersona(agentId);
        var state = new AgentGrainState
        {
            Persona = persona,
            SimulationId = Guid.NewGuid(),
            Status = AgentStatus.Active,
            TicksProcessed = 5
        };

        _memoryStoreMock
            .Setup(m => m.SearchMemoryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        _graphStoreMock
            .Setup(g => g.GetNeighboursAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GraphNode>());

        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object,
            agentId);

        // Act
        await grain.ProcessTickAsync(CreateTestTick());

        // Assert
        Assert.Equal(6, state.TicksProcessed);
    }

    [Fact]
    public async Task SuppressAsync_SetsStatusToSuppressed()
    {
        // Arrange
        var state = new AgentGrainState { Status = AgentStatus.Active };
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        await grain.SuppressAsync();

        // Assert
        Assert.Equal(AgentStatus.Suppressed, state.Status);
    }

    [Fact]
    public async Task ReactivateAsync_SetsStatusToActive()
    {
        // Arrange
        var state = new AgentGrainState { Status = AgentStatus.Suppressed };
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        await grain.ReactivateAsync();

        // Assert
        Assert.Equal(AgentStatus.Active, state.Status);
    }

    [Fact]
    public async Task GetPersonaAsync_ReturnsPersona()
    {
        // Arrange
        var persona = CreateTestPersona();
        var state = new AgentGrainState { Persona = persona };
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        var result = await grain.GetPersonaAsync();

        // Assert
        Assert.Equal(persona, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WhenNotInitialised_ReturnsNull()
    {
        // Arrange
        var state = new AgentGrainState();
        _stateMock.SetupGet(s => s.State).Returns(state);

        var grain = new TestableAgentGrain(
            _stateMock.Object, _memoryStoreMock.Object, _graphStoreMock.Object, _kernel, _loggerMock.Object);

        // Act
        var result = await grain.GetPersonaAsync();

        // Assert
        Assert.Null(result);
    }
}

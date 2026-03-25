using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Orleans.TestingHost;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using Xunit;

namespace SwarmFish.Agents.Orleans.Tests;

public class AgentGrainTests : IClassFixture<AgentGrainTests.ClusterFixture>
{
    public class ClusterFixture : IDisposable
    {
        public TestCluster Cluster { get; }
        public Mock<IMemoryStore> MemoryStoreMock { get; } = new();
        public Mock<IGraphStore> GraphStoreMock { get; } = new();

        public ClusterFixture()
        {
            SiloConfigurator.MemoryStoreMock = MemoryStoreMock;
            SiloConfigurator.GraphStoreMock = GraphStoreMock;

            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            builder.AddClientBuilderConfigurator<ClientConfigurator>();
            Cluster = builder.Build();
            Cluster.Deploy();
        }

        public void Dispose()
        {
            Cluster.StopAllSilos();
        }

        public class SiloConfigurator : ISiloConfigurator
        {
            public static Mock<IMemoryStore> MemoryStoreMock { get; set; } = null!;
            public static Mock<IGraphStore> GraphStoreMock { get; set; } = null!;

            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.AddMemoryGrainStorageAsDefault();
                siloBuilder.AddMemoryGrainStorage("agentState");
                
                siloBuilder.Services.AddSerializer(serializerBuilder =>
                {
                    serializerBuilder.AddJsonSerializer(
                        isSupported: type => type.Namespace != null && type.Namespace.StartsWith("SwarmFish.Core.Contracts"));
                });

                siloBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(MemoryStoreMock.Object);
                    services.AddSingleton(GraphStoreMock.Object);
                    services.AddSingleton(Kernel.CreateBuilder().Build());
                });
            }
        }
        
        public class ClientConfigurator : IClientBuilderConfigurator
        {
            public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
            {
                clientBuilder.Services.AddSerializer(serializerBuilder =>
                {
                    serializerBuilder.AddJsonSerializer(
                        isSupported: type => type.Namespace != null && type.Namespace.StartsWith("SwarmFish.Core.Contracts"));
                });
            }
        }
    }

    private readonly ClusterFixture _fixture;
    private readonly IGrainFactory _grainFactory;

    public AgentGrainTests(ClusterFixture fixture)
    {
        _fixture = fixture;
        _grainFactory = fixture.Cluster.GrainFactory;
        
        _fixture.MemoryStoreMock.Invocations.Clear();
        _fixture.GraphStoreMock.Invocations.Clear();
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
        var agentId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);
        var persona = CreateTestPersona(agentId);
        var simulationId = Guid.NewGuid();

        await grain.InitialiseAsync(persona, simulationId);

        var savedPersona = await grain.GetPersonaAsync();
        Assert.NotNull(savedPersona);
        Assert.Equal(persona.Name, savedPersona.Name);

        _fixture.MemoryStoreMock.Verify(m => m.AppendMemoryAsync(
            persona.Id,
            It.Is<MemoryEntry>(e => e.Type == MemoryEntryType.SeedFact && e.Content.Contains(persona.Name)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTickAsync_WhenSuppressed_ReturnsSilentEvent()
    {
        var agentId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);
        await grain.InitialiseAsync(CreateTestPersona(agentId), Guid.NewGuid());
        await grain.SuppressAsync();

        var result = await grain.ProcessTickAsync(CreateTestTick(), CancellationToken.None);

        Assert.Equal("silent", result.EventType);
    }

    [Fact]
    public async Task ProcessTickAsync_WhenNotInitialised_ThrowsInvalidOperationException()
    {
        var agentId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.ProcessTickAsync(CreateTestTick(), CancellationToken.None));
    }

    [Fact]
    public async Task ProcessTickAsync_QueriesMemoryAndGraph()
    {
        var agentId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);
        await grain.InitialiseAsync(CreateTestPersona(agentId), Guid.NewGuid());

        _fixture.MemoryStoreMock
            .Setup(m => m.SearchMemoryAsync(agentId, It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>
            {
                new("Previous observation", DateTimeOffset.UtcNow, MemoryEntryType.Observation)
            });

        _fixture.GraphStoreMock
            .Setup(g => g.GetNeighboursAsync(agentId.ToString(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GraphNode>());

        var result = await grain.ProcessTickAsync(CreateTestTick(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agentId, result.AgentId);

        _fixture.MemoryStoreMock.Verify(m => m.SearchMemoryAsync(
            agentId, It.IsAny<string>(), 5, It.IsAny<CancellationToken>()), Times.Once);

        _fixture.GraphStoreMock.Verify(g => g.GetNeighboursAsync(
            agentId.ToString(), 2, It.IsAny<CancellationToken>()), Times.Once);

        _fixture.MemoryStoreMock.Verify(m => m.AppendMemoryAsync(
            agentId,
            It.IsAny<MemoryEntry>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReactivateAsync_AllowsTickProcessingAgain()
    {
        var agentId = Guid.NewGuid();
        var grain = _grainFactory.GetGrain<IAgentGrain>(agentId);
        await grain.InitialiseAsync(CreateTestPersona(agentId), Guid.NewGuid());
        await grain.SuppressAsync();
        
        var suppressedResult = await grain.ProcessTickAsync(CreateTestTick(), CancellationToken.None);
        Assert.Equal("silent", suppressedResult.EventType);
        
        await grain.ReactivateAsync();
        var activeResult = await grain.ProcessTickAsync(CreateTestTick(), CancellationToken.None);

        _fixture.MemoryStoreMock.Verify(m => m.SearchMemoryAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

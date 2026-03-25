using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans.Runtime;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Agents.Orleans.Tests;

/// <summary>
/// A testable wrapper around <see cref="AgentGrain"/> that bypasses the Orleans
/// grain lifecycle (activation, state persistence) for pure unit testing.
/// </summary>
internal class TestableAgentGrain : AgentGrain
{
    private readonly Guid _agentId;

    public TestableAgentGrain(
        IPersistentState<AgentGrainState> state,
        IMemoryStore memoryStore,
        IGraphStore graphStore,
        Kernel kernel,
        ILogger<AgentGrain> logger,
        Guid? agentId = null)
        : base(state, memoryStore, graphStore, kernel, logger)
    {
        _agentId = agentId ?? Guid.NewGuid();
    }

    /// <summary>
    /// Overrides <see cref="Grain.GetPrimaryKey()"/> to return the test agent ID.
    /// </summary>
    /// <returns>The test agent GUID.</returns>
    public new Guid GetPrimaryKey() => _agentId;
}

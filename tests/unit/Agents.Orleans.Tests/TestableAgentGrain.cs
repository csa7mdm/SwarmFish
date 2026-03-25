using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SwarmFish.Agents.Orleans.Grains;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Agents.Orleans.Tests;

/// <summary>
/// A testable wrapper around <see cref="AgentGrain"/> that bypasses the Orleans
/// grain lifecycle (activation, state persistence) for pure unit testing.
/// </summary>
internal class TestableAgentGrain : AgentGrain
{
    private readonly AgentGrainState _directState;
    private readonly Guid _agentId;

    public TestableAgentGrain(
        AgentGrainState state,
        IMemoryStore memoryStore,
        IGraphStore graphStore,
        Kernel kernel,
        ILogger<AgentGrain> logger,
        Guid? agentId = null)
        : base(memoryStore, graphStore, kernel, logger)
    {
        _directState = state;
        _agentId = agentId ?? Guid.NewGuid();
    }

    /// <summary>
    /// Overrides the Orleans grain state to use our directly-managed state object.
    /// </summary>
    protected new AgentGrainState State => _directState;

    /// <summary>
    /// Overrides <see cref="Grain.GetPrimaryKey()"/> to return the test agent ID.
    /// </summary>
    /// <returns>The test agent GUID.</returns>
    public new Guid GetPrimaryKey() => _agentId;

    /// <summary>
    /// No-op — bypasses Orleans state persistence.
    /// </summary>
    protected new Task WriteStateAsync() => Task.CompletedTask;
}

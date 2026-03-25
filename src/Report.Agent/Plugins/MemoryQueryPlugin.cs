using System.ComponentModel;
using Microsoft.SemanticKernel;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Report.Agent.Plugins;

/// <summary>
/// Plugin for searching agent memories.
/// </summary>
public class MemoryQueryPlugin
{
    private readonly IMemoryStore _memoryStore;

    public MemoryQueryPlugin(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    [KernelFunction]
    [Description("Search an agent's memories for relevant content")]
    public async Task<string> SearchAgentMemoryAsync(
        [Description("The unique identifier of the agent")] string agentId,
        [Description("The search query")] string query,
        CancellationToken ct = default)
    {
        try
        {
            if (!Guid.TryParse(agentId, out var guid))
            {
                return "Invalid Agent ID format. Must be a Guid.";
            }

            var memories = await _memoryStore.SearchMemoryAsync(guid, query, 10, ct);
            return string.Join("\n---\n", memories.Select(m => m.Content));
        }
        catch (Exception ex)
        {
            return $"Error searching memory: {ex.Message}";
        }
    }
}

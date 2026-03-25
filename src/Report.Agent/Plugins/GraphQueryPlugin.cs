using System.ComponentModel;
using Microsoft.SemanticKernel;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Report.Agent.Plugins;

/// <summary>
/// Plugin for querying the simulation knowledge graph.
/// </summary>
public class GraphQueryPlugin
{
    private readonly IGraphStore _graphStore;

    public GraphQueryPlugin(IGraphStore graphStore)
    {
        _graphStore = graphStore;
    }

    [KernelFunction]
    [Description("Query the simulation knowledge graph using Cypher")]
    public async Task<string> QueryGraphAsync(
        [Description("The Cypher query to execute")] string query,
        CancellationToken ct = default)
    {
        try
        {
            var results = await _graphStore.QueryAsync(query, ct);
            if (results == null || results.Count == 0)
            {
                return "No results found.";
            }

            return string.Join("\n---\n", results.Select(n => 
                $"Node ID: {n.Id}, Label: {n.Label}, Properties: {string.Join(", ", n.Properties.Select(p => $"{p.Key}={p.Value}"))}"));
        }
        catch (Exception ex)
        {
            return $"Error querying graph: {ex.Message}";
        }
    }
}

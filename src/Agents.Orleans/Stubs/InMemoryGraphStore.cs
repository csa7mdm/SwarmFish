using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Agents.Orleans.Stubs;

/// <summary>
/// In-memory stub implementation of <see cref="IGraphStore"/> for development and testing.
/// Will be replaced by the real KuzuDB-backed implementation from Agent C.
/// </summary>
public sealed class InMemoryGraphStore : IGraphStore
{
    private readonly Dictionary<string, GraphNode> _nodes = new();
    private readonly List<GraphEdge> _edges = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<GraphNode>> QueryAsync(string cypher, CancellationToken ct)
    {
        // Stub: return all nodes (real implementation would parse Cypher)
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<GraphNode>>(_nodes.Values.ToList().AsReadOnly());
        }
    }

    /// <inheritdoc />
    public Task UpsertNodeAsync(GraphNode node, CancellationToken ct)
    {
        lock (_lock)
        {
            _nodes[node.Id] = node;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct)
    {
        lock (_lock)
        {
            _edges.RemoveAll(e =>
                e.SourceId == edge.SourceId &&
                e.TargetId == edge.TargetId &&
                e.RelationType == edge.RelationType);
            _edges.Add(edge);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<GraphNode?> GetNodeByIdAsync(string id, CancellationToken ct)
    {
        lock (_lock)
        {
            _nodes.TryGetValue(id, out var node);
            return Task.FromResult(node);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GraphNode>> GetNeighboursAsync(string nodeId, int depth, CancellationToken ct)
    {
        lock (_lock)
        {
            // Stub: BFS-style neighbour traversal up to the specified depth
            var visited = new HashSet<string>();
            var queue = new Queue<(string Id, int CurrentDepth)>();
            queue.Enqueue((nodeId, 0));
            visited.Add(nodeId);

            var result = new List<GraphNode>();

            while (queue.Count > 0)
            {
                var (currentId, currentDepth) = queue.Dequeue();
                if (currentDepth >= depth) continue;

                var neighbourIds = _edges
                    .Where(e => e.SourceId == currentId || e.TargetId == currentId)
                    .Select(e => e.SourceId == currentId ? e.TargetId : e.SourceId)
                    .Where(id => !visited.Contains(id));

                foreach (var neighbourId in neighbourIds)
                {
                    visited.Add(neighbourId);
                    if (_nodes.TryGetValue(neighbourId, out var neighbourNode))
                    {
                        result.Add(neighbourNode);
                    }
                    queue.Enqueue((neighbourId, currentDepth + 1));
                }
            }

            return Task.FromResult<IReadOnlyList<GraphNode>>(result.AsReadOnly());
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

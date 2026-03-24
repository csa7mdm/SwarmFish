namespace SwarmFish.Core.Contracts.Interfaces;

/// <summary>
/// Contract for graph database query and mutation operations.
/// </summary>
public interface IGraphStore : IAsyncDisposable
{
    /// <summary>
    /// Executes a Cypher query and returns matching nodes.
    /// </summary>
    /// <param name="cypher">The Cypher query string.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only list of matching graph nodes.</returns>
    Task<IReadOnlyList<GraphNode>> QueryAsync(string cypher, CancellationToken ct);

    /// <summary>
    /// Inserts or updates a node in the graph.
    /// </summary>
    /// <param name="node">The node to upsert.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task UpsertNodeAsync(GraphNode node, CancellationToken ct);

    /// <summary>
    /// Inserts or updates an edge in the graph.
    /// </summary>
    /// <param name="edge">The edge to upsert.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct);

    /// <summary>
    /// Retrieves a node by its unique identifier.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The matching node, or null if not found.</returns>
    Task<GraphNode?> GetNodeByIdAsync(string id, CancellationToken ct);

    /// <summary>
    /// Retrieves neighbouring nodes up to a specified depth.
    /// </summary>
    /// <param name="nodeId">The starting node identifier.</param>
    /// <param name="depth">The maximum traversal depth.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only list of neighbouring graph nodes.</returns>
    Task<IReadOnlyList<GraphNode>> GetNeighboursAsync(string nodeId, int depth, CancellationToken ct);
}

/// <summary>
/// Represents a node in the knowledge graph.
/// </summary>
/// <param name="Id">The unique identifier of the node.</param>
/// <param name="Label">The label/type of the node.</param>
/// <param name="Properties">Key-value properties attached to the node.</param>
public record GraphNode(string Id, string Label, IReadOnlyDictionary<string, object> Properties);

/// <summary>
/// Represents a directed edge in the knowledge graph.
/// </summary>
/// <param name="SourceId">The identifier of the source node.</param>
/// <param name="TargetId">The identifier of the target node.</param>
/// <param name="RelationType">The type of relationship this edge represents.</param>
/// <param name="Properties">Key-value properties attached to the edge.</param>
public record GraphEdge(string SourceId, string TargetId, string RelationType, IReadOnlyDictionary<string, object> Properties);

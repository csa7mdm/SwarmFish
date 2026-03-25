using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SwarmFish.Core.Contracts.Interfaces;

namespace SwarmFish.Graph.KuzuDB;

/// <summary>
/// Implements <see cref="IGraphStore"/> using KuzuDB as the backing graph database.
/// Provides schema initialisation, CRUD operations, GraphRAG retrieval, bulk import,
/// and read-path caching.
/// </summary>
public sealed class KuzuGraphStore : IGraphStore
{
    private readonly KuzuConnectionPool _pool;
    private readonly ILogger _logger;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private bool _schemaInitialised;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);

    /// <summary>
    /// Initialises a new KuzuGraphStore instance.
    /// </summary>
    /// <param name="pool">The connection pool to use for database access.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="cache">Memory cache for hot read-path caching.</param>
    public KuzuGraphStore(KuzuConnectionPool pool, ILogger logger, IMemoryCache cache)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        };
    }

    /// <summary>
    /// Ensures the graph schema is initialised. Idempotent — safe to call multiple times.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaInitialised) return;

        await _schemaLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_schemaInitialised) return;

            using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

            var ddlStatements = new[]
            {
                "CREATE NODE TABLE IF NOT EXISTS Entity(id STRING, label STRING, properties STRING, PRIMARY KEY(id))",
                "CREATE NODE TABLE IF NOT EXISTS Agent(id STRING, personaJson STRING, simulationId STRING, PRIMARY KEY(id))",
                "CREATE REL TABLE IF NOT EXISTS KNOWS(FROM Agent TO Agent, weight FLOAT, context STRING)",
                "CREATE REL TABLE IF NOT EXISTS MENTIONED_IN(FROM Entity TO Entity, source STRING)",
                "CREATE REL TABLE IF NOT EXISTS HAS_TRAIT(FROM Agent TO Entity, strength FLOAT)"
            };

            foreach (var ddl in ddlStatements)
            {
                lease.Connection.ExecuteNonQuery(ddl);
            }

            _schemaInitialised = true;
            _logger.LogInformation("KuzuDB schema initialised");
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> QueryAsync(string cypher, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);
        var rows = lease.Connection.Execute(cypher);
        return RowsToNodes(rows);
    }

    /// <inheritdoc />
    public async Task UpsertNodeAsync(GraphNode node, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var tableName = GetTableName(node.Label);
        var propsJson = JsonSerializer.Serialize(node.Properties);
        var escapedProps = EscapeCypher(propsJson);
        var escapedId = EscapeCypher(node.Id);

        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        if (tableName == "Agent")
        {
            var personaJson = node.Properties.TryGetValue("personaJson", out var p) ? p?.ToString() ?? "" : "";
            var simulationId = node.Properties.TryGetValue("simulationId", out var s) ? s?.ToString() ?? "" : "";
            var escapedPersona = EscapeCypher(personaJson);
            var escapedSimId = EscapeCypher(simulationId);

            // Try merge — if node exists update, otherwise create
            var cypher = $"MERGE (n:Agent {{id: '{escapedId}'}}) " +
                         $"SET n.personaJson = '{escapedPersona}', n.simulationId = '{escapedSimId}'";
            lease.Connection.ExecuteNonQuery(cypher);
        }
        else
        {
            var escapedLabel = EscapeCypher(node.Label);
            var cypher = $"MERGE (n:Entity {{id: '{escapedId}'}}) " +
                         $"SET n.label = '{escapedLabel}', n.properties = '{escapedProps}'";
            lease.Connection.ExecuteNonQuery(cypher);
        }

        // Invalidate caches for this node
        InvalidateCacheForNode(node.Id);

        _logger.LogDebug("Upserted node {Id} (table: {Table})", node.Id, tableName);
    }

    /// <inheritdoc />
    public async Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var escapedSourceId = EscapeCypher(edge.SourceId);
        var escapedTargetId = EscapeCypher(edge.TargetId);
        var relType = edge.RelationType.ToUpperInvariant();

        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        // Determine source/target tables based on relation type
        var (sourceTable, targetTable) = relType switch
        {
            "KNOWS" => ("Agent", "Agent"),
            "MENTIONED_IN" => ("Entity", "Entity"),
            "HAS_TRAIT" => ("Agent", "Entity"),
            _ => ("Entity", "Entity")
        };

        // Build property SET clause
        var propClauses = BuildEdgePropertyClause(edge);

        var cypher = $"MATCH (a:{sourceTable} {{id: '{escapedSourceId}'}}), (b:{targetTable} {{id: '{escapedTargetId}'}}) " +
                     $"MERGE (a)-[r:{relType}]->(b) {propClauses}";

        lease.Connection.ExecuteNonQuery(cypher);

        // Invalidate caches for both end nodes
        InvalidateCacheForNode(edge.SourceId);
        InvalidateCacheForNode(edge.TargetId);

        _logger.LogDebug("Upserted edge {Source}-[{Type}]->{Target}", edge.SourceId, relType, edge.TargetId);
    }

    /// <inheritdoc />
    public async Task<GraphNode?> GetNodeByIdAsync(string id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var escapedId = EscapeCypher(id);
        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        // Try Entity table first
        var rows = lease.Connection.Execute(
            $"MATCH (n:Entity {{id: '{escapedId}'}}) RETURN n.id, n.label, n.properties");
        if (rows.Count > 0)
        {
            return RowToEntityNode(rows[0]);
        }

        // Try Agent table
        rows = lease.Connection.Execute(
            $"MATCH (n:Agent {{id: '{escapedId}'}}) RETURN n.id, n.personaJson, n.simulationId");
        if (rows.Count > 0)
        {
            return RowToAgentNode(rows[0]);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphNode>> GetNeighboursAsync(string nodeId, int depth, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var cacheKey = $"neighbours:{nodeId}:{depth}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<GraphNode>? cached) && cached != null)
        {
            return cached;
        }

        var escapedId = EscapeCypher(nodeId);
        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        // Multi-hop query across all relationship types
        // Use variable-length path pattern
        var results = new List<GraphNode>();

        // Query from Agent table
        try
        {
            var agentRows = lease.Connection.Execute(
                $"MATCH (start:Agent {{id: '{escapedId}'}})-[*1..{depth}]-(neighbour) " +
                $"RETURN DISTINCT neighbour.id, neighbour.personaJson, neighbour.simulationId, neighbour.label, neighbour.properties");
            results.AddRange(RowsToMixedNodes(agentRows));
        }
        catch (InvalidOperationException)
        {
            // Starting node might not be in Agent table
        }

        // Query from Entity table
        try
        {
            var entityRows = lease.Connection.Execute(
                $"MATCH (start:Entity {{id: '{escapedId}'}})-[*1..{depth}]-(neighbour) " +
                $"RETURN DISTINCT neighbour.id, neighbour.label, neighbour.properties, neighbour.personaJson, neighbour.simulationId");
            results.AddRange(RowsToMixedNodes(entityRows));
        }
        catch (InvalidOperationException)
        {
            // Starting node might not be in Entity table
        }

        // Deduplicate by node Id
        var deduplicated = results
            .GroupBy(n => n.Id)
            .Select(g => g.First())
            .Where(n => n.Id != nodeId) // Exclude the starting node
            .ToList()
            .AsReadOnly();

        _cache.Set(cacheKey, deduplicated, _cacheOptions);
        return deduplicated;
    }

    /// <summary>
    /// Retrieves a structured GraphRAG context string for the given agent, summarising
    /// their graph neighbourhood up to the specified hop depth.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <param name="hopDepth">Maximum traversal depth (default 2).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A formatted summary string for LLM context window injection.</returns>
    public async Task<string> GetPersonaContextAsync(Guid agentId, int hopDepth = 2, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var agentIdStr = agentId.ToString();
        var cacheKey = $"persona:{agentIdStr}:{hopDepth}";

        if (_cache.TryGetValue(cacheKey, out string? cachedContext) && cachedContext != null)
        {
            return cachedContext;
        }

        var escapedId = EscapeCypher(agentIdStr);
        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        var sb = new StringBuilder();

        // Get the agent's own identity
        var agentRows = lease.Connection.Execute(
            $"MATCH (a:Agent {{id: '{escapedId}'}}) RETURN a.id, a.personaJson");
        var agentName = agentIdStr;
        if (agentRows.Count > 0 && agentRows[0].TryGetValue("a.personaJson", out var personaObj))
        {
            var personaStr = personaObj?.ToString() ?? "";
            if (!string.IsNullOrEmpty(personaStr))
            {
                try
                {
                    using var doc = JsonDocument.Parse(personaStr);
                    if (doc.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        agentName = nameElement.GetString() ?? agentIdStr;
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON, use the raw string
                    agentName = personaStr;
                }
            }
        }

        // Get KNOWS relationships
        var knowsRows = lease.Connection.Execute(
            $"MATCH (a:Agent {{id: '{escapedId}'}})-[r:KNOWS]->(b:Agent) " +
            $"RETURN b.id, b.personaJson, r.context");
        var knownAgents = new List<string>();
        foreach (var row in knowsRows)
        {
            var bId = GetStringValue(row, "b.id");
            var bPersona = GetStringValue(row, "b.personaJson");
            var context = GetStringValue(row, "r.context");
            var name = ExtractNameFromPersona(bPersona) ?? bId ?? "Unknown";
            knownAgents.Add(string.IsNullOrEmpty(context) ? name : $"{name} ({context})");
        }

        // Get HAS_TRAIT relationships
        var traitRows = lease.Connection.Execute(
            $"MATCH (a:Agent {{id: '{escapedId}'}})-[r:HAS_TRAIT]->(e:Entity) " +
            $"RETURN e.id, e.label, r.strength");
        var traits = new List<string>();
        foreach (var row in traitRows)
        {
            var entityLabel = GetStringValue(row, "e.label");
            var entityId = GetStringValue(row, "e.id");
            var label = !string.IsNullOrEmpty(entityLabel) ? entityLabel : (entityId ?? "Unknown");
            traits.Add(label);
        }

        // Get related entities via multi-hop
        var entityRows = lease.Connection.Execute(
            $"MATCH (a:Agent {{id: '{escapedId}'}})-[*1..{hopDepth}]-(e:Entity) " +
            $"RETURN DISTINCT e.id, e.label");
        var entities = new List<string>();
        foreach (var row in entityRows)
        {
            var entityLabel = GetStringValue(row, "e.label");
            var entityId = GetStringValue(row, "e.id");
            var label = !string.IsNullOrEmpty(entityLabel) ? entityLabel : (entityId ?? "Unknown");
            if (!traits.Contains(label))
            {
                entities.Add(label);
            }
        }

        // Build the context string
        sb.Append($"{agentName}");
        if (knownAgents.Count > 0)
        {
            sb.Append($" knows: {string.Join(", ", knownAgents)}.");
        }
        if (entities.Count > 0)
        {
            sb.Append($" Relevant entities: {string.Join(", ", entities)}.");
        }
        if (traits.Count > 0)
        {
            sb.Append($" Key relationships: {string.Join(", ", traits)}.");
        }

        var result = sb.ToString();
        _cache.Set(cacheKey, result, _cacheOptions);

        _logger.LogDebug("Generated persona context for agent {AgentId}: {Context}", agentId, result);
        return result;
    }

    /// <summary>
    /// Performs a bulk import of nodes and edges using CSV temp files and KuzuDB's COPY FROM.
    /// This is the fastest path for loading large datasets from the ingestion pipeline.
    /// </summary>
    /// <param name="nodes">The nodes to import.</param>
    /// <param name="edges">The edges to import.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    public async Task BulkImportAsync(
        IEnumerable<GraphNode> nodes,
        IEnumerable<GraphEdge> edges,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var tempDir = Path.Combine(Path.GetTempPath(), $"kuzu_bulk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var nodeList = nodes.ToList();
            var edgeList = edges.ToList();

            _logger.LogInformation("Starting bulk import: {NodeCount} nodes, {EdgeCount} edges",
                nodeList.Count, edgeList.Count);

            // Separate nodes by table
            var entityNodes = nodeList.Where(n => GetTableName(n.Label) != "Agent").ToList();
            var agentNodes = nodeList.Where(n => GetTableName(n.Label) == "Agent").ToList();

            using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

            // Bulk import Entity nodes
            if (entityNodes.Count > 0)
            {
                var entityCsvPath = Path.Combine(tempDir, "entities.csv");
                await WriteEntityCsvAsync(entityCsvPath, entityNodes, ct).ConfigureAwait(false);
                lease.Connection.ExecuteNonQuery(
                    $"COPY Entity FROM '{entityCsvPath.Replace("\\", "/")}' (HEADER=true, DELIM=',')");
                _logger.LogInformation("Bulk imported {Count} Entity nodes", entityNodes.Count);
            }

            // Bulk import Agent nodes
            if (agentNodes.Count > 0)
            {
                var agentCsvPath = Path.Combine(tempDir, "agents.csv");
                await WriteAgentCsvAsync(agentCsvPath, agentNodes, ct).ConfigureAwait(false);
                lease.Connection.ExecuteNonQuery(
                    $"COPY Agent FROM '{agentCsvPath.Replace("\\", "/")}' (HEADER=true, DELIM=',')");
                _logger.LogInformation("Bulk imported {Count} Agent nodes", agentNodes.Count);
            }

            // Bulk import edges by relationship type
            var edgeGroups = edgeList.GroupBy(e => e.RelationType.ToUpperInvariant());
            foreach (var group in edgeGroups)
            {
                var relType = group.Key;
                var edgesInGroup = group.ToList();
                var csvPath = Path.Combine(tempDir, $"edges_{relType.ToLowerInvariant()}.csv");

                await WriteEdgeCsvAsync(csvPath, edgesInGroup, relType, ct).ConfigureAwait(false);

                var (sourceTable, targetTable) = relType switch
                {
                    "KNOWS" => ("Agent", "Agent"),
                    "MENTIONED_IN" => ("Entity", "Entity"),
                    "HAS_TRAIT" => ("Agent", "Entity"),
                    _ => ("Entity", "Entity")
                };

                lease.Connection.ExecuteNonQuery(
                    $"COPY {relType} FROM '{csvPath.Replace("\\", "/")}' (HEADER=true, DELIM=',')");
                _logger.LogInformation("Bulk imported {Count} {RelType} edges", edgesInGroup.Count, relType);
            }

            _logger.LogInformation("Bulk import completed");
        }
        finally
        {
            // Clean up temp directory
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp directory: {Dir}", tempDir);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _pool.DisposeAsync().ConfigureAwait(false);
    }

    // ── Private helpers ─────────────────────────────────────────────

    private static string GetTableName(string label)
    {
        return label.Equals("Agent", StringComparison.OrdinalIgnoreCase) ? "Agent" : "Entity";
    }

    private static string EscapeCypher(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }

    private static string BuildEdgePropertyClause(GraphEdge edge)
    {
        var setClauses = new List<string>();
        foreach (var kvp in edge.Properties)
        {
            var key = kvp.Key;
            var value = kvp.Value;
            if (value is float or double or int or long)
            {
                setClauses.Add($"r.{key} = {value}");
            }
            else
            {
                setClauses.Add($"r.{key} = '{EscapeCypher(value?.ToString() ?? "")}'");
            }
        }

        return setClauses.Count > 0 ? $"SET {string.Join(", ", setClauses)}" : "";
    }

    private void InvalidateCacheForNode(string nodeId)
    {
        // Remove any cached entries that include this node
        // We use a simple approach — remove known key patterns
        for (var depth = 1; depth <= 5; depth++)
        {
            _cache.Remove($"neighbours:{nodeId}:{depth}");
        }
        // Persona context uses Guid format, try removing
        _cache.Remove($"persona:{nodeId}:1");
        _cache.Remove($"persona:{nodeId}:2");
        _cache.Remove($"persona:{nodeId}:3");
    }

    private static IReadOnlyList<GraphNode> RowsToNodes(List<Dictionary<string, object>> rows)
    {
        return rows.Select(row =>
        {
            var id = GetStringValue(row, "n.id") ?? GetStringValue(row, "id") ?? "";
            var label = GetStringValue(row, "n.label") ?? GetStringValue(row, "label") ?? "Entity";
            var propsStr = GetStringValue(row, "n.properties") ?? GetStringValue(row, "properties");

            var props = ParseProperties(propsStr);
            // Add any other columns as properties
            foreach (var kvp in row)
            {
                if (!kvp.Key.StartsWith("n.") && !props.ContainsKey(kvp.Key))
                {
                    props[kvp.Key] = kvp.Value;
                }
            }

            return new GraphNode(id, label, props);
        }).ToList().AsReadOnly();
    }

    private static IReadOnlyList<GraphNode> RowsToMixedNodes(List<Dictionary<string, object>> rows)
    {
        var results = new List<GraphNode>();
        foreach (var row in rows)
        {
            var id = GetStringValue(row, "neighbour.id") ?? "";
            if (string.IsNullOrEmpty(id)) continue;

            var personaJson = GetStringValue(row, "neighbour.personaJson");
            var simId = GetStringValue(row, "neighbour.simulationId");
            var label = GetStringValue(row, "neighbour.label");
            var propsStr = GetStringValue(row, "neighbour.properties");

            if (!string.IsNullOrEmpty(personaJson))
            {
                var props = new Dictionary<string, object>
                {
                    ["personaJson"] = personaJson,
                    ["simulationId"] = simId ?? ""
                };
                results.Add(new GraphNode(id, "Agent", props));
            }
            else
            {
                var props = ParseProperties(propsStr);
                results.Add(new GraphNode(id, label ?? "Entity", props));
            }
        }

        return results;
    }

    private static GraphNode RowToEntityNode(Dictionary<string, object> row)
    {
        var id = GetStringValue(row, "n.id") ?? "";
        var label = GetStringValue(row, "n.label") ?? "Entity";
        var propsStr = GetStringValue(row, "n.properties");
        var props = ParseProperties(propsStr);
        return new GraphNode(id, label, props);
    }

    private static GraphNode RowToAgentNode(Dictionary<string, object> row)
    {
        var id = GetStringValue(row, "n.id") ?? "";
        var personaJson = GetStringValue(row, "n.personaJson") ?? "";
        var simulationId = GetStringValue(row, "n.simulationId") ?? "";
        var props = new Dictionary<string, object>
        {
            ["personaJson"] = personaJson,
            ["simulationId"] = simulationId
        };
        return new GraphNode(id, "Agent", props);
    }

    private static string? GetStringValue(Dictionary<string, object> row, string key)
    {
        if (row.TryGetValue(key, out var value) && value is string s && s != DBNull.Value.ToString())
        {
            return s;
        }
        return null;
    }

    private static Dictionary<string, object> ParseProperties(string? propsStr)
    {
        if (string.IsNullOrEmpty(propsStr))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propsStr);
            if (parsed == null) return new Dictionary<string, object>();

            var result = new Dictionary<string, object>();
            foreach (var kvp in parsed)
            {
                result[kvp.Key] = kvp.Value.ValueKind switch
                {
                    JsonValueKind.String => kvp.Value.GetString() ?? "",
                    JsonValueKind.Number => kvp.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => kvp.Value.ToString()
                };
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object> { ["raw"] = propsStr };
        }
    }

    private static string? ExtractNameFromPersona(string? personaJson)
    {
        if (string.IsNullOrEmpty(personaJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(personaJson);
            if (doc.RootElement.TryGetProperty("name", out var nameElement))
            {
                return nameElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Not valid JSON
        }
        return null;
    }

    private static async Task WriteEntityCsvAsync(string path, List<GraphNode> nodes, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
        await writer.WriteLineAsync("id,label,properties").ConfigureAwait(false);
        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();
            var propsJson = JsonSerializer.Serialize(node.Properties);
            await writer.WriteLineAsync($"{CsvEscape(node.Id)},{CsvEscape(node.Label)},{CsvEscape(propsJson)}")
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteAgentCsvAsync(string path, List<GraphNode> nodes, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
        await writer.WriteLineAsync("id,personaJson,simulationId").ConfigureAwait(false);
        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();
            var personaJson = node.Properties.TryGetValue("personaJson", out var p) ? p?.ToString() ?? "" : "";
            var simulationId = node.Properties.TryGetValue("simulationId", out var s) ? s?.ToString() ?? "" : "";
            await writer.WriteLineAsync(
                $"{CsvEscape(node.Id)},{CsvEscape(personaJson)},{CsvEscape(simulationId)}")
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteEdgeCsvAsync(
        string path, List<GraphEdge> edges, string relType, CancellationToken ct)
    {
        await using var writer = new StreamWriter(path, false, Encoding.UTF8);

        // Write header based on relation type
        var header = relType switch
        {
            "KNOWS" => "from,to,weight,context",
            "MENTIONED_IN" => "from,to,source",
            "HAS_TRAIT" => "from,to,strength",
            _ => "from,to"
        };
        await writer.WriteLineAsync(header).ConfigureAwait(false);

        foreach (var edge in edges)
        {
            ct.ThrowIfCancellationRequested();
            var line = relType switch
            {
                "KNOWS" =>
                    $"{CsvEscape(edge.SourceId)},{CsvEscape(edge.TargetId)}," +
                    $"{GetNumericProp(edge, "weight")},{CsvEscape(GetStringProp(edge, "context"))}",
                "MENTIONED_IN" =>
                    $"{CsvEscape(edge.SourceId)},{CsvEscape(edge.TargetId)}," +
                    $"{CsvEscape(GetStringProp(edge, "source"))}",
                "HAS_TRAIT" =>
                    $"{CsvEscape(edge.SourceId)},{CsvEscape(edge.TargetId)}," +
                    $"{GetNumericProp(edge, "strength")}",
                _ =>
                    $"{CsvEscape(edge.SourceId)},{CsvEscape(edge.TargetId)}"
            };
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string GetStringProp(GraphEdge edge, string key)
    {
        return edge.Properties.TryGetValue(key, out var val) ? val?.ToString() ?? "" : "";
    }

    private static string GetNumericProp(GraphEdge edge, string key)
    {
        if (edge.Properties.TryGetValue(key, out var val))
        {
            if (val is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (val is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (val is int i) return i.ToString();
            if (val is long l) return l.ToString();
            return val?.ToString() ?? "0";
        }
        return "0";
    }
}

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Graph.KuzuDB;
using Xunit;

namespace Graph.KuzuDB.IntegrationTests;

public class KuzuGraphStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly KuzuConnectionPool _pool;
    private readonly KuzuGraphStore _store;
    private readonly MemoryCache _cache;

    public KuzuGraphStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"kuzu_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbPath);

        _pool = new KuzuConnectionPool(_dbPath, NullLogger.Instance, poolSize: 2);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _store = new KuzuGraphStore(_pool, NullLogger.Instance, _cache);
    }

    [Fact]
    public async Task UpsertAndGetNode_WorksCorrectly()
    {
        // Arrange
        var nodeId = "node_1";
        var node = new GraphNode(nodeId, "Entity", new Dictionary<string, object>
        {
            ["name"] = "Test Entity",
            ["value"] = 42
        });

        // Act
        await _store.UpsertNodeAsync(node, CancellationToken.None);
        var retrieved = await _store.GetNodeByIdAsync(nodeId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(nodeId, retrieved.Id);
        Assert.Equal("Entity", retrieved.Label);
        Assert.Equal("Test Entity", retrieved.Properties["name"].ToString());
        Assert.Equal(42.0, Convert.ToDouble(retrieved.Properties["value"]));
    }

    [Fact]
    public async Task BulkImport_ImportsNodesAndEdgesSuccessfully()
    {
        // Arrange
        var nodes = new List<GraphNode>
        {
            new("agent1", "Agent", new Dictionary<string, object> { ["personaJson"] = "{\"name\":\"Alice\"}" }),
            new("agent2", "Agent", new Dictionary<string, object> { ["personaJson"] = "{\"name\":\"Bob\"}" }),
            new("entity1", "Entity", new Dictionary<string, object> { ["desc"] = "Secret Base" })
        };

        var edges = new List<GraphEdge>
        {
            new("agent1", "agent2", "KNOWS", new Dictionary<string, object> { ["weight"] = 1.0, ["context"] = "friends" }),
            new("agent1", "entity1", "HAS_TRAIT", new Dictionary<string, object> { ["strength"] = 0.9 })
        };

        // Act
        await _store.BulkImportAsync(nodes, edges, CancellationToken.None);

        var alice = await _store.GetNodeByIdAsync("agent1", CancellationToken.None);
        var neighbours = await _store.GetNeighboursAsync("agent1", 1, CancellationToken.None);
        var context = await _store.GetPersonaContextAsync(Guid.Empty, 1, CancellationToken.None); // guid empty will not match but we can just check if nodes exist

        // Assert
        Assert.NotNull(alice);
        Assert.Equal(2, neighbours.Count);
        Assert.Contains(neighbours, n => n.Id == "agent2");
        Assert.Contains(neighbours, n => n.Id == "entity1");
    }

    public void Dispose()
    {
        _pool.Dispose();
        _cache.Dispose();
        
        try
        {
            if (Directory.Exists(_dbPath))
            {
                Directory.Delete(_dbPath, true);
            }
        }
        catch
        {
            // Ignore temp dir deletion errors
        }
    }
}

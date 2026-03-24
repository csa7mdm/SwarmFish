using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SwarmFish.Graph.KuzuDB.Native;

namespace SwarmFish.Graph.KuzuDB;

/// <summary>
/// A thread-safe pool of KuzuDB connections backed by <see cref="Channel{T}"/>.
/// Connections share a single database handle and are leased/returned automatically.
/// </summary>
public sealed class KuzuConnectionPool : IAsyncDisposable, IDisposable
{
    private readonly IntPtr _db;
    private readonly Channel<KuzuConnection> _channel;
    private readonly ILogger _logger;
    private readonly int _poolSize;
    private readonly List<KuzuConnection> _allConnections;
    private bool _disposed;

    /// <summary>
    /// Gets the configured pool size.
    /// </summary>
    public int PoolSize => _poolSize;

    /// <summary>
    /// Initialises a new connection pool with the specified number of connections.
    /// </summary>
    /// <param name="dbPath">Filesystem path for the KuzuDB database directory.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="poolSize">Number of connections to maintain (default 8).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if poolSize is less than 1.</exception>
    /// <exception cref="InvalidOperationException">Thrown if database initialisation fails.</exception>
    public KuzuConnectionPool(string dbPath, ILogger logger, int poolSize = 8)
    {
        if (poolSize < 1) throw new ArgumentOutOfRangeException(nameof(poolSize), "Pool size must be at least 1.");

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _poolSize = poolSize;
        _allConnections = new List<KuzuConnection>(poolSize);

        _db = KuzuNative.kuzu_database_init(dbPath, IntPtr.Zero);
        if (_db == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to initialise KuzuDB at path: {dbPath}");
        }

        _channel = Channel.CreateBounded<KuzuConnection>(new BoundedChannelOptions(poolSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        for (var i = 0; i < poolSize; i++)
        {
            var conn = new KuzuConnection(_db, _logger);
            _allConnections.Add(conn);
            if (!_channel.Writer.TryWrite(conn))
            {
                throw new InvalidOperationException("Failed to seed connection into pool channel.");
            }
        }

        _logger.LogInformation("KuzuDB connection pool initialised with {PoolSize} connections at {DbPath}", poolSize, dbPath);
    }

    /// <summary>
    /// Leases a connection from the pool. The returned <see cref="PooledConnection"/> must be disposed
    /// to return the connection to the pool.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A scoped connection wrapper that auto-returns on dispose.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async Task<PooledConnection> LeaseAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var conn = await _channel.Reader.ReadAsync(ct).ConfigureAwait(false);
        return new PooledConnection(conn, this);
    }

    /// <summary>
    /// Returns a connection to the pool. Called automatically by <see cref="PooledConnection.Dispose"/>.
    /// </summary>
    /// <param name="connection">The connection to return.</param>
    internal void Return(KuzuConnection connection)
    {
        if (_disposed || connection.IsDisposed) return;

        if (!_channel.Writer.TryWrite(connection))
        {
            _logger.LogWarning("Failed to return connection to pool — channel may be full or completed.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Writer.Complete();

        // Drain all connections from the channel and dispose them
        while (_channel.Reader.TryRead(out _))
        {
            // Just drain; we dispose all from _allConnections below
        }

        foreach (var conn in _allConnections)
        {
            conn.Dispose();
        }

        _allConnections.Clear();

        if (_db != IntPtr.Zero)
        {
            KuzuNative.kuzu_database_destroy(_db);
        }

        _logger.LogInformation("KuzuDB connection pool disposed");

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// A scoped wrapper around a leased <see cref="KuzuConnection"/> that
/// automatically returns it to the pool on disposal.
/// </summary>
public sealed class PooledConnection : IDisposable
{
    private readonly KuzuConnectionPool _pool;
    private bool _returned;

    /// <summary>
    /// Gets the underlying KuzuDB connection.
    /// </summary>
    public KuzuConnection Connection { get; }

    /// <summary>
    /// Initialises a new pooled connection wrapper.
    /// </summary>
    /// <param name="connection">The leased connection.</param>
    /// <param name="pool">The owning pool to return to on dispose.</param>
    internal PooledConnection(KuzuConnection connection, KuzuConnectionPool pool)
    {
        Connection = connection;
        _pool = pool;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_returned) return;
        _returned = true;
        _pool.Return(Connection);
    }
}

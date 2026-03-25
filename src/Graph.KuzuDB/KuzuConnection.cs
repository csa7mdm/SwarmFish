using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SwarmFish.Graph.KuzuDB.Native;

namespace SwarmFish.Graph.KuzuDB;

/// <summary>
/// A disposable wrapper around a KuzuDB database + connection pair.
/// Manages native handle lifetimes and provides safe query execution.
/// </summary>
public sealed class KuzuConnection : IDisposable
{
    private IntPtr _db;
    private IntPtr _conn;
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether this connection has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initialises a new KuzuDB connection to the database at the specified path.
    /// </summary>
    /// <param name="dbPath">Filesystem path for the KuzuDB database directory.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <exception cref="InvalidOperationException">Thrown when database or connection initialisation fails.</exception>
    public KuzuConnection(string dbPath, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _db = KuzuNative.kuzu_database_init(dbPath, IntPtr.Zero);
        if (_db == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to initialise KuzuDB at path: {dbPath}");
        }

        _conn = KuzuNative.kuzu_connection_init(_db);
        if (_conn == IntPtr.Zero)
        {
            KuzuNative.kuzu_database_destroy(_db);
            _db = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create KuzuDB connection.");
        }

        _logger.LogDebug("KuzuDB connection opened to {DbPath}", dbPath);
    }

    /// <summary>
    /// Initialises a new KuzuDB connection that shares the given database handle.
    /// The caller retains ownership of the database handle — this connection will NOT destroy it.
    /// </summary>
    /// <param name="sharedDb">Shared database handle (not owned by this connection).</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    internal KuzuConnection(IntPtr sharedDb, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _db = IntPtr.Zero; // We do NOT own the db handle

        _conn = KuzuNative.kuzu_connection_init(sharedDb);
        if (_conn == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create KuzuDB connection from shared database.");
        }
    }

    /// <summary>
    /// Executes a Cypher query and returns the results as a list of dictionaries.
    /// Each dictionary maps column names to their values for a single row.
    /// </summary>
    /// <param name="cypher">The Cypher query to execute.</param>
    /// <returns>A list of row dictionaries.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the query fails.</exception>
    public List<Dictionary<string, object>> Execute(string cypher)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = KuzuNative.kuzu_connection_query(_conn, cypher);
        if (result == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Query returned null result: {cypher}");
        }

        try
        {
            if (!KuzuNative.kuzu_query_result_is_success(result))
            {
                var errorPtr = KuzuNative.kuzu_query_result_get_error_message(result);
                var errorMsg = errorPtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(errorPtr) ?? "Unknown error"
                    : "Unknown error";
                throw new InvalidOperationException($"KuzuDB query failed: {errorMsg}");
            }

            var rows = new List<Dictionary<string, object>>();
            var numColumns = KuzuNative.kuzu_query_result_get_num_columns(result);

            // Read column names
            var columnNames = new string[numColumns];
            for (long i = 0; i < numColumns; i++)
            {
                var namePtr = KuzuNative.kuzu_query_result_get_column_name(result, i);
                columnNames[i] = namePtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(namePtr) ?? $"col_{i}"
                    : $"col_{i}";
            }

            // Iterate rows
            while (KuzuNative.kuzu_query_result_has_next(result))
            {
                var tuple = KuzuNative.kuzu_query_result_get_next(result);
                if (tuple == IntPtr.Zero) break;

                try
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    for (long i = 0; i < numColumns; i++)
                    {
                        var value = KuzuNative.kuzu_flat_tuple_get_value(tuple, i);
                        if (value == IntPtr.Zero || KuzuNative.kuzu_value_is_null(value))
                        {
                            row[columnNames[i]] = DBNull.Value;
                            continue;
                        }

                        // Convert all values to string representation for generic handling
                        var strPtr = KuzuNative.kuzu_value_to_string(value);
                        var strValue = strPtr != IntPtr.Zero
                            ? Marshal.PtrToStringAnsi(strPtr) ?? string.Empty
                            : string.Empty;

                        if (strPtr != IntPtr.Zero)
                        {
                            KuzuNative.kuzu_destroy_string(strPtr);
                        }

                        row[columnNames[i]] = strValue;
                    }

                    rows.Add(row);
                }
                finally
                {
                    KuzuNative.kuzu_flat_tuple_destroy(tuple);
                }
            }

            _logger.LogDebug("Query returned {RowCount} rows: {Query}", rows.Count, cypher);
            return rows;
        }
        finally
        {
            KuzuNative.kuzu_query_result_destroy(result);
        }
    }

    /// <summary>
    /// Executes a Cypher statement that produces no result rows (DDL, mutations).
    /// </summary>
    /// <param name="cypher">The Cypher statement to execute.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the connection has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the statement fails.</exception>
    public void ExecuteNonQuery(string cypher)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = KuzuNative.kuzu_connection_query(_conn, cypher);
        if (result == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Query returned null result: {cypher}");
        }

        try
        {
            if (!KuzuNative.kuzu_query_result_is_success(result))
            {
                var errorPtr = KuzuNative.kuzu_query_result_get_error_message(result);
                var errorMsg = errorPtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(errorPtr) ?? "Unknown error"
                    : "Unknown error";
                throw new InvalidOperationException($"KuzuDB statement failed: {errorMsg}");
            }
        }
        finally
        {
            KuzuNative.kuzu_query_result_destroy(result);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_conn != IntPtr.Zero)
        {
            KuzuNative.kuzu_connection_destroy(_conn);
            _conn = IntPtr.Zero;
        }

        // Only destroy the database if we own it (created via dbPath constructor)
        if (_db != IntPtr.Zero)
        {
            KuzuNative.kuzu_database_destroy(_db);
            _db = IntPtr.Zero;
        }

        _logger.LogDebug("KuzuDB connection disposed");
    }
}

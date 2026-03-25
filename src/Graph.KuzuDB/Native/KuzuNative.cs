using System.Runtime.InteropServices;

namespace SwarmFish.Graph.KuzuDB.Native;

/// <summary>
/// P/Invoke declarations for the KuzuDB C API.
/// All pointers returned by kuzu_*_init functions must be freed
/// by their corresponding kuzu_*_destroy functions.
/// </summary>
internal static class KuzuNative
{
    private const string LibName = "kuzu";

    // ── Database lifecycle ──────────────────────────────────────────

    /// <summary>
    /// Initialises a new KuzuDB database instance at the given path.
    /// </summary>
    /// <param name="dbPath">Filesystem path for the database directory.</param>
    /// <param name="systemConfig">Pointer to a kuzu_system_config struct, or IntPtr.Zero for defaults.</param>
    /// <returns>Pointer to the database instance, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr kuzu_database_init(string dbPath, IntPtr systemConfig);

    /// <summary>
    /// Destroys a KuzuDB database instance and frees associated resources.
    /// </summary>
    /// <param name="db">Pointer to the database instance.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_database_destroy(IntPtr db);

    // ── Connection lifecycle ────────────────────────────────────────

    /// <summary>
    /// Creates a new connection to the given database instance.
    /// </summary>
    /// <param name="db">Pointer to the database instance.</param>
    /// <returns>Pointer to the connection, or IntPtr.Zero on failure.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_connection_init(IntPtr db);

    /// <summary>
    /// Destroys a connection and frees associated resources.
    /// </summary>
    /// <param name="conn">Pointer to the connection.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_connection_destroy(IntPtr conn);

    // ── Query execution ─────────────────────────────────────────────

    /// <summary>
    /// Executes a Cypher query on the given connection.
    /// </summary>
    /// <param name="conn">Pointer to the connection.</param>
    /// <param name="query">The Cypher query string.</param>
    /// <returns>Pointer to the query result, which must be freed via kuzu_query_result_destroy.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr kuzu_connection_query(IntPtr conn, string query);

    // ── Query result inspection ─────────────────────────────────────

    /// <summary>
    /// Destroys a query result and frees associated resources.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_query_result_destroy(IntPtr result);

    /// <summary>
    /// Checks whether the query executed successfully.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>True if the query was successful.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool kuzu_query_result_is_success(IntPtr result);

    /// <summary>
    /// Gets the error message for a failed query.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>Pointer to the error message string.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_query_result_get_error_message(IntPtr result);

    /// <summary>
    /// Gets the number of columns in the query result.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>The number of columns.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long kuzu_query_result_get_num_columns(IntPtr result);

    /// <summary>
    /// Gets the name of a column by index.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <param name="index">Zero-based column index.</param>
    /// <returns>Pointer to the column name string.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_query_result_get_column_name(IntPtr result, long index);

    /// <summary>
    /// Checks whether there are more rows to iterate.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>True if more rows are available.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool kuzu_query_result_has_next(IntPtr result);

    /// <summary>
    /// Advances to the next row and returns a flat tuple.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>Pointer to the flat tuple for the current row.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_query_result_get_next(IntPtr result);

    /// <summary>
    /// Gets the number of tuples (rows) in the result.
    /// </summary>
    /// <param name="result">Pointer to the query result.</param>
    /// <returns>Number of result tuples.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long kuzu_query_result_get_num_tuples(IntPtr result);

    // ── Flat tuple value access ─────────────────────────────────────

    /// <summary>
    /// Gets a value from a flat tuple at the given index.
    /// </summary>
    /// <param name="flatTuple">Pointer to the flat tuple.</param>
    /// <param name="index">Zero-based value index.</param>
    /// <returns>Pointer to the value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_flat_tuple_get_value(IntPtr flatTuple, long index);

    /// <summary>
    /// Destroys a flat tuple and frees associated resources.
    /// </summary>
    /// <param name="flatTuple">Pointer to the flat tuple.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_flat_tuple_destroy(IntPtr flatTuple);

    // ── Value inspection ────────────────────────────────────────────

    /// <summary>
    /// Converts a KuzuDB value to its string representation.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>Pointer to the string representation.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_value_to_string(IntPtr value);

    /// <summary>
    /// Gets the boolean value.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>The boolean value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool kuzu_value_get_bool(IntPtr value);

    /// <summary>
    /// Gets the int64 value.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>The int64 value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern long kuzu_value_get_int64(IntPtr value);

    /// <summary>
    /// Gets the double value.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>The double value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern double kuzu_value_get_double(IntPtr value);

    /// <summary>
    /// Gets the float value.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>The float value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern float kuzu_value_get_float(IntPtr value);

    /// <summary>
    /// Gets the string value.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>Pointer to the string value.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr kuzu_value_get_string(IntPtr value);

    /// <summary>
    /// Checks whether a value is null.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    /// <returns>True if the value is null.</returns>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool kuzu_value_is_null(IntPtr value);

    /// <summary>
    /// Destroys a value and frees associated resources.
    /// </summary>
    /// <param name="value">Pointer to the value.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_value_destroy(IntPtr value);

    // ── Memory management ───────────────────────────────────────────

    /// <summary>
    /// Frees a string allocated by the KuzuDB C API.
    /// </summary>
    /// <param name="str">Pointer to the string to free.</param>
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void kuzu_destroy_string(IntPtr str);
}

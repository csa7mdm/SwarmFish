using Microsoft.Extensions.Logging;

namespace SwarmFish.Graph.KuzuDB;

/// <summary>
/// Runs Cypher migration scripts from the Migrations directory against a KuzuDB instance.
/// Tracks applied migrations in a <c>_Migrations</c> node table to ensure idempotency.
/// </summary>
public sealed class KuzuMigrationRunner
{
    private readonly KuzuConnectionPool _pool;
    private readonly ILogger _logger;
    private readonly string _migrationsDirectory;

    /// <summary>
    /// Initialises a new migration runner.
    /// </summary>
    /// <param name="pool">The connection pool to execute migrations against.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    /// <param name="migrationsDirectory">
    /// Path to the directory containing <c>*.cypher</c> migration scripts.
    /// If null, defaults to the <c>Migrations</c> subdirectory next to this assembly.
    /// </param>
    public KuzuMigrationRunner(KuzuConnectionPool pool, ILogger<KuzuMigrationRunner> logger, string? migrationsDirectory = null)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _migrationsDirectory = migrationsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Migrations");
    }

    /// <summary>
    /// Ensures the <c>_Migrations</c> tracking table exists.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    public async Task EnsureMigrationTableAsync(CancellationToken ct = default)
    {
        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);
        lease.Connection.ExecuteNonQuery(
            "CREATE NODE TABLE IF NOT EXISTS _Migrations(name STRING, appliedAt STRING, PRIMARY KEY(name))");
        _logger.LogDebug("Migration tracking table ensured");
    }

    /// <summary>
    /// Runs all pending migration scripts in filename order.
    /// Already-applied migrations are skipped.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>The number of migrations applied.</returns>
    public async Task<int> RunMigrationsAsync(CancellationToken ct = default)
    {
        await EnsureMigrationTableAsync(ct).ConfigureAwait(false);

        if (!Directory.Exists(_migrationsDirectory))
        {
            _logger.LogWarning("Migrations directory not found: {Dir}", _migrationsDirectory);
            return 0;
        }

        var migrationFiles = Directory.GetFiles(_migrationsDirectory, "*.cypher")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (migrationFiles.Count == 0)
        {
            _logger.LogInformation("No migration files found");
            return 0;
        }

        // Get already-applied migrations
        var applied = await GetAppliedMigrationsAsync(ct).ConfigureAwait(false);

        var appliedCount = 0;
        foreach (var file in migrationFiles)
        {
            var name = Path.GetFileName(file);
            if (applied.Contains(name))
            {
                _logger.LogDebug("Skipping already-applied migration: {Name}", name);
                continue;
            }

            await ApplyMigrationAsync(file, name, ct).ConfigureAwait(false);
            appliedCount++;
        }

        _logger.LogInformation("Applied {Count} migration(s)", appliedCount);
        return appliedCount;
    }

    /// <summary>
    /// Gets the set of already-applied migration names.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A set of applied migration filenames.</returns>
    public async Task<HashSet<string>> GetAppliedMigrationsAsync(CancellationToken ct = default)
    {
        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rows = lease.Connection.Execute("MATCH (m:_Migrations) RETURN m.name");
            foreach (var row in rows)
            {
                if (row.TryGetValue("m.name", out var nameObj) && nameObj is string name)
                {
                    result.Add(name);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Table may not exist yet — treat as empty
            _logger.LogDebug("_Migrations table query failed — assuming no migrations applied yet");
        }

        return result;
    }

    private async Task ApplyMigrationAsync(string filePath, string name, CancellationToken ct)
    {
        _logger.LogInformation("Applying migration: {Name}", name);

        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var statements = content
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        using var lease = await _pool.LeaseAsync(ct).ConfigureAwait(false);

        foreach (var statement in statements)
        {
            _logger.LogDebug("Executing migration statement: {Statement}", statement);
            lease.Connection.ExecuteNonQuery(statement);
        }

        // Record the migration as applied
        var escapedName = name.Replace("'", "\\'");
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        lease.Connection.ExecuteNonQuery(
            $"CREATE (m:_Migrations {{name: '{escapedName}', appliedAt: '{timestamp}'}})");

        _logger.LogInformation("Migration applied: {Name}", name);
    }
}

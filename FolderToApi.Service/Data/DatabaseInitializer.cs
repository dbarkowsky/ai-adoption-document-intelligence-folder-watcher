using FolderToApi.Service.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace FolderToApi.Service.Data;

/// <summary>
/// Handles SQLite database initialization, schema creation, and migrations
/// </summary>
public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseInitializer> _logger;

    private const int CurrentSchemaVersion = 1;

    public DatabaseInitializer(
        IOptions<DatabaseOptions> options,
        ILogger<DatabaseInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        _logger.LogInformation("Initializing database...");

        try
        {
            // Create connection to initialize database file
            using var connection = new SqliteConnection(_options.ConnectionString);
            await connection.OpenAsync();

            // Enable WAL mode for better concurrency
            if (_options.EnableWalMode)
            {
                await EnableWalModeAsync(connection);
            }

            // Create schema version table if it doesn't exist
            await CreateSchemaVersionTableAsync(connection);

            // Get current schema version
            int currentVersion = await GetCurrentSchemaVersionAsync(connection);
            _logger.LogInformation("Current database schema version: {Version}", currentVersion);

            // Apply migrations
            if (currentVersion < CurrentSchemaVersion)
            {
                _logger.LogInformation("Applying schema migrations from version {From} to {To}",
                    currentVersion, CurrentSchemaVersion);

                for (int version = currentVersion + 1; version <= CurrentSchemaVersion; version++)
                {
                    await ApplyMigrationAsync(connection, version);
                }

                _logger.LogInformation("Schema migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database schema is up to date");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            return false;
        }
    }

    public async Task<int> GetCurrentSchemaVersionAsync()
    {
        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();
        return await GetCurrentSchemaVersionAsync(connection);
    }

    private async Task EnableWalModeAsync(SqliteConnection connection)
    {
        _logger.LogDebug("Enabling WAL mode for better concurrency");

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        var result = await command.ExecuteScalarAsync();

        _logger.LogInformation("Database journal mode set to: {Mode}", result);
    }

    private async Task CreateSchemaVersionTableAsync(SqliteConnection connection)
    {
        _logger.LogDebug("Creating schema_version table if it doesn't exist");

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );";

        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> GetCurrentSchemaVersionAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task ApplyMigrationAsync(SqliteConnection connection, int version)
    {
        _logger.LogInformation("Applying migration for schema version {Version}", version);

        using var transaction = connection.BeginTransaction();

        try
        {
            switch (version)
            {
                case 1:
                    await ApplyMigration_V1_InitialSchema(connection);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown migration version: {version}");
            }

            // Record the migration
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = @"
                INSERT INTO schema_version (version, applied_at_utc)
                VALUES (@version, @appliedAt);";
            versionCommand.Parameters.AddWithValue("@version", version);
            versionCommand.Parameters.AddWithValue("@appliedAt", DateTime.UtcNow.ToString("O"));
            await versionCommand.ExecuteNonQueryAsync();

            transaction.Commit();
            _logger.LogInformation("Migration for version {Version} applied successfully", version);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task ApplyMigration_V1_InitialSchema(SqliteConnection connection)
    {
        _logger.LogInformation("Creating initial database schema (v1)");

        // Create jobs table
        using var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS jobs (
                job_id TEXT PRIMARY KEY,
                source_path TEXT NOT NULL,
                processing_path TEXT,
                status TEXT NOT NULL,
                discovered_at_utc TEXT NOT NULL,
                claimed_at_utc TEXT,
                sent_at_utc TEXT,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_at_utc TEXT,
                last_error TEXT,
                last_http_status INTEGER,
                file_size_bytes INTEGER,
                file_mtime_utc TEXT
            );";

        await createTableCommand.ExecuteNonQueryAsync();
        _logger.LogInformation("Created jobs table");

        // Create indexes for query performance
        using var createIndexCommand1 = connection.CreateCommand();
        createIndexCommand1.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_jobs_status_next_attempt
            ON jobs(status, next_attempt_at_utc);";
        await createIndexCommand1.ExecuteNonQueryAsync();
        _logger.LogInformation("Created index: idx_jobs_status_next_attempt");

        using var createIndexCommand2 = connection.CreateCommand();
        createIndexCommand2.CommandText = @"
            CREATE UNIQUE INDEX IF NOT EXISTS idx_jobs_processing_path
            ON jobs(processing_path) WHERE processing_path IS NOT NULL;";
        await createIndexCommand2.ExecuteNonQueryAsync();
        _logger.LogInformation("Created index: idx_jobs_processing_path");

        _logger.LogInformation("Initial schema (v1) created successfully");
    }
}

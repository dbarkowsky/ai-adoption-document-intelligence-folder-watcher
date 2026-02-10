using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace FolderToApi.Service.Services;

/// <summary>
/// Manages the folder structure for the file delivery service
/// </summary>
public class FolderStructureService : IFolderStructureService
{
    private readonly FolderWatcherOptions _options;
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<FolderStructureService> _logger;

    public FolderStructureService(
        IOptions<FolderWatcherOptions> options,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<FolderStructureService> logger)
    {
        _options = options.Value;
        _databaseOptions = databaseOptions.Value;
        _logger = logger;

        InboxPath = Path.Combine(_options.RootPath, "inbox");
        ProcessingPath = Path.Combine(_options.RootPath, "processing");
        SentPath = Path.Combine(_options.RootPath, "sent");
        FailedPath = Path.Combine(_options.RootPath, "failed");
    }

    public string InboxPath { get; }
    public string ProcessingPath { get; }
    public string SentPath { get; }
    public string FailedPath { get; }

    public async Task<bool> InitializeAsync()
    {
        _logger.LogInformation("Initializing folder structure with RootPath: {RootPath}", _options.RootPath);

        try
        {
            // Validate root path is reachable
            if (!await ValidateRootPathAsync())
            {
                return false;
            }

            // Create required folders
            if (!await CreateRequiredFoldersAsync())
            {
                return false;
            }

            // Validate folder permissions
            if (!await ValidateFolderPermissionsAsync())
            {
                return false;
            }

            // Validate database location
            if (!ValidateDatabaseLocation())
            {
                return false;
            }

            _logger.LogInformation("Folder structure initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize folder structure");
            return false;
        }
    }

    private async Task<bool> ValidateRootPathAsync()
    {
        _logger.LogDebug("Validating root path: {RootPath}", _options.RootPath);

        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            _logger.LogError("RootPath is not configured");
            return false;
        }

        // Check if path is UNC or local
        bool isUnc = _options.RootPath.StartsWith(@"\\") || _options.RootPath.StartsWith("//");
        _logger.LogInformation("RootPath type: {PathType}", isUnc ? "UNC (network share)" : "Local");

        // Try to access the root path (with retry for network paths)
        int retryCount = isUnc ? 3 : 1;
        int retryDelayMs = 2000;

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                // Try to get directory info to verify accessibility
                var dirInfo = new DirectoryInfo(_options.RootPath);

                // For UNC paths, this might throw if share is unreachable
                _ = dirInfo.Exists;

                if (!dirInfo.Exists)
                {
                    _logger.LogInformation("RootPath does not exist, will attempt to create it: {RootPath}", _options.RootPath);
                    dirInfo.Create();
                    _logger.LogInformation("Created RootPath: {RootPath}", _options.RootPath);
                }

                _logger.LogInformation("RootPath is accessible: {RootPath}", _options.RootPath);
                return true;
            }
            catch (Exception ex) when (i < retryCount - 1)
            {
                _logger.LogWarning(ex, "Failed to access RootPath (attempt {Attempt}/{Total}), retrying in {Delay}ms...",
                    i + 1, retryCount, retryDelayMs);
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RootPath is not accessible after {Attempts} attempts: {RootPath}",
                    retryCount, _options.RootPath);
                return false;
            }
        }

        return false;
    }

    private async Task<bool> CreateRequiredFoldersAsync()
    {
        var folders = new[]
        {
            ("inbox", InboxPath),
            ("processing", ProcessingPath),
            ("sent", SentPath),
            ("failed", FailedPath)
        };

        foreach (var (name, path) in folders)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    _logger.LogInformation("Creating {FolderName} folder: {Path}", name, path);
                    Directory.CreateDirectory(path);

                    // Verify creation
                    await Task.Delay(100); // Small delay for network paths

                    if (!Directory.Exists(path))
                    {
                        _logger.LogError("Failed to verify creation of {FolderName} folder: {Path}", name, path);
                        return false;
                    }

                    _logger.LogInformation("Successfully created {FolderName} folder: {Path}", name, path);
                }
                else
                {
                    _logger.LogDebug("{FolderName} folder already exists: {Path}", name, path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create {FolderName} folder: {Path}", name, path);
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ValidateFolderPermissionsAsync()
    {
        var folders = new[]
        {
            ("inbox", InboxPath),
            ("processing", ProcessingPath),
            ("sent", SentPath),
            ("failed", FailedPath)
        };

        bool allPermissionsValid = true;

        foreach (var (name, path) in folders)
        {
            try
            {
                // Test read permission
                _ = Directory.EnumerateFiles(path).Take(1).ToList();

                // Test write permission by creating and deleting a test file
                string testFileName = $".permission_test_{Guid.NewGuid()}.tmp";
                string testFilePath = Path.Combine(path, testFileName);

                await File.WriteAllTextAsync(testFilePath, "permission test");

                if (!File.Exists(testFilePath))
                {
                    _logger.LogError("Permission validation failed for {FolderName}: Cannot verify file creation at {Path}",
                        name, path);
                    allPermissionsValid = false;
                    continue;
                }

                File.Delete(testFilePath);

                _logger.LogDebug("Permissions validated for {FolderName} folder: {Path}", name, path);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Insufficient permissions for {FolderName} folder: {Path}", name, path);
                allPermissionsValid = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate permissions for {FolderName} folder: {Path}", name, path);
                allPermissionsValid = false;
            }
        }

        if (!allPermissionsValid)
        {
            _logger.LogError("Folder permission validation failed. Ensure the service account has Read/Write/Modify permissions on all folders.");
        }

        return allPermissionsValid;
    }

    private bool ValidateDatabaseLocation()
    {
        _logger.LogDebug("Validating database location: {ConnectionString}", _databaseOptions.ConnectionString);

        try
        {
            // Extract file path from connection string
            string? dbPath = ExtractDatabasePath(_databaseOptions.ConnectionString);

            if (string.IsNullOrWhiteSpace(dbPath))
            {
                _logger.LogWarning("Could not extract database path from connection string: {ConnectionString}",
                    _databaseOptions.ConnectionString);
                return true; // Allow it to proceed, might be in-memory or special case
            }

            // Make path absolute if it's relative
            if (!Path.IsPathRooted(dbPath))
            {
                dbPath = Path.GetFullPath(dbPath);
                _logger.LogInformation("Resolved relative database path to: {Path}", dbPath);
            }

            // Check if path is UNC
            bool isUnc = dbPath.StartsWith(@"\\") || dbPath.StartsWith("//");

            if (isUnc)
            {
                _logger.LogError("SQLite database is configured on a UNC path (network share): {Path}. " +
                    "This is not recommended for reliability. Please use a local disk path.", dbPath);
                return false;
            }

            // Ensure the directory exists
            string? dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                _logger.LogInformation("Creating database directory: {Directory}", dbDirectory);
                Directory.CreateDirectory(dbDirectory);
            }

            _logger.LogInformation("Database location validated (local path): {Path}", dbPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate database location");
            return false;
        }
    }

    private string? ExtractDatabasePath(string connectionString)
    {
        // Simple parser for SQLite connection strings
        // Supports: "Data Source=path" or "DataSource=path" or "Filename=path"
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2)
            {
                string key = keyValue[0].Trim();
                string value = keyValue[1].Trim();

                if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("DataSource", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Filename", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }

        return null;
    }
}

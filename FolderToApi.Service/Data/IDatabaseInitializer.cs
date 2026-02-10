namespace FolderToApi.Service.Data;

/// <summary>
/// Handles database schema creation and migrations
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Initializes the database schema and applies any pending migrations
    /// </summary>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Gets the current schema version
    /// </summary>
    Task<int> GetCurrentSchemaVersionAsync();
}

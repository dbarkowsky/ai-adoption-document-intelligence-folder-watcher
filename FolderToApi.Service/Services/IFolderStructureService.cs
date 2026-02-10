namespace FolderToApi.Service.Services;

/// <summary>
/// Manages the folder structure for the file delivery service
/// </summary>
public interface IFolderStructureService
{
    /// <summary>
    /// Initializes and validates the required folder structure
    /// </summary>
    /// <returns>True if initialization was successful</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Gets the path to the inbox folder
    /// </summary>
    string InboxPath { get; }

    /// <summary>
    /// Gets the path to the processing folder
    /// </summary>
    string ProcessingPath { get; }

    /// <summary>
    /// Gets the path to the sent folder
    /// </summary>
    string SentPath { get; }

    /// <summary>
    /// Gets the path to the failed folder
    /// </summary>
    string FailedPath { get; }
}

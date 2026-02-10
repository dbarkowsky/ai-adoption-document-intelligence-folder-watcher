using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Checks if files are complete and ready for processing
/// </summary>
public interface ICompletenessChecker
{
    /// <summary>
    /// Checks if a file is complete and not locked by another process
    /// </summary>
    /// <param name="file">The file to check</param>
    /// <returns>Completeness check result</returns>
    CompletenessCheckResult CheckFile(IFileInfo file);
}

/// <summary>
/// Result of a file completeness check
/// </summary>
public class CompletenessCheckResult
{
    public FileCompletenessStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAtUtc { get; set; }

    public bool IsComplete => Status == FileCompletenessStatus.Complete;
}

/// <summary>
/// Status codes for file completeness checks
/// </summary>
public enum FileCompletenessStatus
{
    /// <summary>File is complete and ready for processing</summary>
    Complete,

    /// <summary>File is locked by another process</summary>
    Locked,

    /// <summary>File is too new (within stability age window)</summary>
    TooNew,

    /// <summary>File disappeared during check</summary>
    Gone,

    /// <summary>An error occurred during the check</summary>
    Error
}

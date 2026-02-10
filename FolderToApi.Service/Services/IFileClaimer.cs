using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Claims files by moving them from inbox to processing
/// </summary>
public interface IFileClaimer
{
    /// <summary>
    /// Claims a file by moving it from inbox to processing folder
    /// </summary>
    /// <param name="sourceFile">The file to claim</param>
    /// <param name="jobId">The job ID for this file</param>
    /// <returns>Claim result</returns>
    Task<ClaimResult> ClaimFileAsync(IFileInfo sourceFile, string jobId);
}

/// <summary>
/// Result of a file claim operation
/// </summary>
public class ClaimResult
{
    public ClaimStatus Status { get; set; }
    public string? ProcessingPath { get; set; }
    public string? ErrorMessage { get; set; }
    public bool UsedCrossVolumeFallback { get; set; }

    public bool IsSuccess => Status == ClaimStatus.Success;
}

/// <summary>
/// Status codes for file claim operations
/// </summary>
public enum ClaimStatus
{
    /// <summary>File successfully claimed</summary>
    Success,

    /// <summary>File was already claimed</summary>
    AlreadyClaimed,

    /// <summary>Transient failure, can retry</summary>
    TransientFailure,

    /// <summary>Permanent failure, cannot retry</summary>
    PermanentFailure
}

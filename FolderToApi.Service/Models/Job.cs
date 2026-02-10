namespace FolderToApi.Service.Models;

/// <summary>
/// Represents a file processing job
/// </summary>
public class Job
{
    public string JobId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string? ProcessingPath { get; set; }
    public JobStatus Status { get; set; }
    public DateTime DiscoveredAtUtc { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public int? LastHttpStatus { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime? FileMtimeUtc { get; set; }
}

/// <summary>
/// Job lifecycle status values
/// </summary>
public enum JobStatus
{
    /// <summary>File discovered in inbox, not yet claimed</summary>
    Discovered,

    /// <summary>File moved to processing folder, ready to send</summary>
    Claimed,

    /// <summary>Currently attempting to send</summary>
    Sending,

    /// <summary>Send failed, scheduled for retry</summary>
    RetryScheduled,

    /// <summary>Successfully sent to API</summary>
    Sent,

    /// <summary>Permanently failed (dead letter)</summary>
    Failed
}

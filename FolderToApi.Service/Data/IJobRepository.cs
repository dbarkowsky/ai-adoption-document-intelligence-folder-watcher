using FolderToApi.Service.Models;

namespace FolderToApi.Service.Data;

/// <summary>
/// Repository for managing job persistence and lifecycle
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Creates a new job record for a discovered file
    /// </summary>
    Task<Job> CreateJobAsync(string jobId, string sourcePath, long fileSizeBytes, DateTime fileMtimeUtc);

    /// <summary>
    /// Updates a job to Claimed status after moving to processing folder
    /// </summary>
    Task UpdateJobClaimedAsync(string jobId, string processingPath, DateTime claimedAtUtc);

    /// <summary>
    /// Marks a job as Sending before attempting delivery
    /// </summary>
    Task MarkJobSendingAsync(string jobId);

    /// <summary>
    /// Records a retry attempt with error details and schedules next attempt
    /// </summary>
    Task RecordRetryAsync(string jobId, string error, int? httpStatus, DateTime nextAttemptAtUtc);

    /// <summary>
    /// Marks a job as successfully sent
    /// </summary>
    Task MarkJobSentAsync(string jobId, DateTime sentAtUtc, int? httpStatus);

    /// <summary>
    /// Marks a job as permanently failed (dead letter)
    /// </summary>
    Task MarkJobFailedAsync(string jobId, string error, int? httpStatus);

    /// <summary>
    /// Gets all jobs that are due for sending (Claimed or RetryScheduled with next_attempt_at <= now)
    /// </summary>
    Task<List<Job>> GetDueJobsAsync(DateTime currentTimeUtc, int limit = 100);

    /// <summary>
    /// Finds a job by its source path
    /// </summary>
    Task<Job?> FindJobBySourcePathAsync(string sourcePath);

    /// <summary>
    /// Finds a job by its job ID
    /// </summary>
    Task<Job?> FindJobByIdAsync(string jobId);

    /// <summary>
    /// Gets job statistics for monitoring
    /// </summary>
    Task<JobStatistics> GetStatisticsAsync();

    /// <summary>
    /// Queries failed jobs for manual review and troubleshooting
    /// </summary>
    /// <param name="limit">Maximum number of failed jobs to return</param>
    /// <returns>List of failed jobs ordered by discovered_at_utc descending</returns>
    Task<List<Job>> GetFailedJobsAsync(int limit = 100);

    /// <summary>
    /// Recovers orphaned jobs that were in Sending status (likely from service crash/restart)
    /// Resets them to RetryScheduled status with immediate next attempt
    /// </summary>
    /// <returns>Number of jobs recovered</returns>
    Task<int> RecoverOrphanedSendingJobsAsync();
}

/// <summary>
/// Job statistics for monitoring
/// </summary>
public class JobStatistics
{
    public int TotalJobs { get; set; }
    public int DiscoveredCount { get; set; }
    public int ClaimedCount { get; set; }
    public int SendingCount { get; set; }
    public int RetryScheduledCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? OldestPendingJobTime { get; set; }
}

using FolderToApi.Service.Configuration;
using FolderToApi.Service.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace FolderToApi.Service.Data;

/// <summary>
/// SQLite-based job repository with transactional state management
/// </summary>
public class JobRepository : IJobRepository
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<JobRepository> _logger;

    public JobRepository(
        IOptions<DatabaseOptions> options,
        ILogger<JobRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Job> CreateJobAsync(string jobId, string sourcePath, long fileSizeBytes, DateTime fileMtimeUtc)
    {
        _logger.LogDebug("Creating job {JobId} for file: {SourcePath}", jobId, sourcePath);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO jobs (
                    job_id, source_path, status, discovered_at_utc,
                    attempt_count, file_size_bytes, file_mtime_utc
                ) VALUES (
                    @jobId, @sourcePath, @status, @discoveredAt,
                    0, @fileSize, @fileMtime
                );";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@sourcePath", sourcePath);
            command.Parameters.AddWithValue("@status", JobStatus.Discovered.ToString());
            command.Parameters.AddWithValue("@discoveredAt", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("@fileSize", fileSizeBytes);
            command.Parameters.AddWithValue("@fileMtime", fileMtimeUtc.ToString("O"));

            await command.ExecuteNonQueryAsync();

            transaction.Commit();

            _logger.LogInformation("Created job {JobId} for file: {SourcePath}", jobId, sourcePath);

            return new Job
            {
                JobId = jobId,
                SourcePath = sourcePath,
                Status = JobStatus.Discovered,
                DiscoveredAtUtc = DateTime.UtcNow,
                AttemptCount = 0,
                FileSizeBytes = fileSizeBytes,
                FileMtimeUtc = fileMtimeUtc
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateJobClaimedAsync(string jobId, string processingPath, DateTime claimedAtUtc)
    {
        _logger.LogDebug("Updating job {JobId} to Claimed status", jobId);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE jobs
                SET status = @status,
                    processing_path = @processingPath,
                    claimed_at_utc = @claimedAt,
                    next_attempt_at_utc = @nextAttempt
                WHERE job_id = @jobId;";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@status", JobStatus.Claimed.ToString());
            command.Parameters.AddWithValue("@processingPath", processingPath);
            command.Parameters.AddWithValue("@claimedAt", claimedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("@nextAttempt", DateTime.UtcNow.ToString("O")); // Ready to send immediately

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            transaction.Commit();

            _logger.LogInformation("Updated job {JobId} to Claimed status with processing path: {ProcessingPath}",
                jobId, processingPath);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task MarkJobSendingAsync(string jobId)
    {
        _logger.LogDebug("Marking job {JobId} as Sending", jobId);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE jobs
                SET status = @status,
                    attempt_count = attempt_count + 1
                WHERE job_id = @jobId;";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@status", JobStatus.Sending.ToString());

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            transaction.Commit();

            _logger.LogDebug("Marked job {JobId} as Sending", jobId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task RecordRetryAsync(string jobId, string error, int? httpStatus, DateTime nextAttemptAtUtc)
    {
        _logger.LogDebug("Recording retry for job {JobId}, next attempt at {NextAttempt}",
            jobId, nextAttemptAtUtc);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE jobs
                SET status = @status,
                    last_error = @error,
                    last_http_status = @httpStatus,
                    next_attempt_at_utc = @nextAttempt
                WHERE job_id = @jobId;";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@status", JobStatus.RetryScheduled.ToString());
            command.Parameters.AddWithValue("@error", error);
            command.Parameters.AddWithValue("@httpStatus", (object?)httpStatus ?? DBNull.Value);
            command.Parameters.AddWithValue("@nextAttempt", nextAttemptAtUtc.ToString("O"));

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            transaction.Commit();

            _logger.LogInformation("Recorded retry for job {JobId}, next attempt at {NextAttempt}",
                jobId, nextAttemptAtUtc);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task MarkJobSentAsync(string jobId, DateTime sentAtUtc, int? httpStatus)
    {
        _logger.LogDebug("Marking job {JobId} as Sent", jobId);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE jobs
                SET status = @status,
                    sent_at_utc = @sentAt,
                    last_http_status = @httpStatus,
                    next_attempt_at_utc = NULL
                WHERE job_id = @jobId;";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@status", JobStatus.Sent.ToString());
            command.Parameters.AddWithValue("@sentAt", sentAtUtc.ToString("O"));
            command.Parameters.AddWithValue("@httpStatus", (object?)httpStatus ?? DBNull.Value);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            transaction.Commit();

            _logger.LogInformation("Marked job {JobId} as Sent", jobId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task MarkJobFailedAsync(string jobId, string error, int? httpStatus)
    {
        _logger.LogDebug("Marking job {JobId} as Failed", jobId);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE jobs
                SET status = @status,
                    last_error = @error,
                    last_http_status = @httpStatus,
                    next_attempt_at_utc = NULL
                WHERE job_id = @jobId;";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@status", JobStatus.Failed.ToString());
            command.Parameters.AddWithValue("@error", error);
            command.Parameters.AddWithValue("@httpStatus", (object?)httpStatus ?? DBNull.Value);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            transaction.Commit();

            _logger.LogWarning("Marked job {JobId} as Failed: {Error}", jobId, error);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<Job>> GetDueJobsAsync(DateTime currentTimeUtc, int limit = 100)
    {
        _logger.LogDebug("Querying due jobs (limit: {Limit})", limit);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, source_path, processing_path, status, discovered_at_utc,
                   claimed_at_utc, sent_at_utc, attempt_count, next_attempt_at_utc,
                   last_error, last_http_status, file_size_bytes, file_mtime_utc
            FROM jobs
            WHERE status IN (@claimed, @retryScheduled)
              AND next_attempt_at_utc <= @currentTime
            ORDER BY next_attempt_at_utc
            LIMIT @limit;";

        command.Parameters.AddWithValue("@claimed", JobStatus.Claimed.ToString());
        command.Parameters.AddWithValue("@retryScheduled", JobStatus.RetryScheduled.ToString());
        command.Parameters.AddWithValue("@currentTime", currentTimeUtc.ToString("O"));
        command.Parameters.AddWithValue("@limit", limit);

        var jobs = new List<Job>();

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            jobs.Add(ReadJob(reader));
        }

        _logger.LogDebug("Found {Count} due jobs", jobs.Count);

        return jobs;
    }

    public async Task<Job?> FindJobBySourcePathAsync(string sourcePath)
    {
        _logger.LogDebug("Finding job by source path: {SourcePath}", sourcePath);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, source_path, processing_path, status, discovered_at_utc,
                   claimed_at_utc, sent_at_utc, attempt_count, next_attempt_at_utc,
                   last_error, last_http_status, file_size_bytes, file_mtime_utc
            FROM jobs
            WHERE source_path = @sourcePath
            LIMIT 1;";

        command.Parameters.AddWithValue("@sourcePath", sourcePath);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadJob(reader);
        }

        return null;
    }

    public async Task<Job?> FindJobByIdAsync(string jobId)
    {
        _logger.LogDebug("Finding job by ID: {JobId}", jobId);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, source_path, processing_path, status, discovered_at_utc,
                   claimed_at_utc, sent_at_utc, attempt_count, next_attempt_at_utc,
                   last_error, last_http_status, file_size_bytes, file_mtime_utc
            FROM jobs
            WHERE job_id = @jobId;";

        command.Parameters.AddWithValue("@jobId", jobId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadJob(reader);
        }

        return null;
    }

    public async Task<JobStatistics> GetStatisticsAsync()
    {
        _logger.LogDebug("Fetching job statistics");

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                COUNT(*) as total,
                SUM(CASE WHEN status = @discovered THEN 1 ELSE 0 END) as discovered,
                SUM(CASE WHEN status = @claimed THEN 1 ELSE 0 END) as claimed,
                SUM(CASE WHEN status = @sending THEN 1 ELSE 0 END) as sending,
                SUM(CASE WHEN status = @retryScheduled THEN 1 ELSE 0 END) as retry_scheduled,
                SUM(CASE WHEN status = @sent THEN 1 ELSE 0 END) as sent,
                SUM(CASE WHEN status = @failed THEN 1 ELSE 0 END) as failed,
                MIN(CASE WHEN status IN (@discovered, @claimed, @retryScheduled) THEN discovered_at_utc ELSE NULL END) as oldest_pending
            FROM jobs;";

        command.Parameters.AddWithValue("@discovered", JobStatus.Discovered.ToString());
        command.Parameters.AddWithValue("@claimed", JobStatus.Claimed.ToString());
        command.Parameters.AddWithValue("@sending", JobStatus.Sending.ToString());
        command.Parameters.AddWithValue("@retryScheduled", JobStatus.RetryScheduled.ToString());
        command.Parameters.AddWithValue("@sent", JobStatus.Sent.ToString());
        command.Parameters.AddWithValue("@failed", JobStatus.Failed.ToString());

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var stats = new JobStatistics
            {
                TotalJobs = reader.GetInt32(0),
                DiscoveredCount = reader.GetInt32(1),
                ClaimedCount = reader.GetInt32(2),
                SendingCount = reader.GetInt32(3),
                RetryScheduledCount = reader.GetInt32(4),
                SentCount = reader.GetInt32(5),
                FailedCount = reader.GetInt32(6)
            };

            if (!reader.IsDBNull(7))
            {
                stats.OldestPendingJobTime = DateTime.Parse(reader.GetString(7));
            }

            return stats;
        }

        return new JobStatistics();
    }

    public async Task<List<Job>> GetFailedJobsAsync(int limit = 100)
    {
        _logger.LogDebug("Querying failed jobs with limit={Limit}", limit);

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                job_id, source_path, processing_path, status, discovered_at_utc,
                claimed_at_utc, sent_at_utc, attempt_count, next_attempt_at_utc,
                last_error, last_http_status, file_size_bytes, file_mtime_utc
            FROM jobs
            WHERE status = @status
            ORDER BY discovered_at_utc DESC
            LIMIT @limit;";

        command.Parameters.AddWithValue("@status", JobStatus.Failed.ToString());
        command.Parameters.AddWithValue("@limit", limit);

        var jobs = new List<Job>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            jobs.Add(ReadJob(reader));
        }

        _logger.LogDebug("Found {Count} failed jobs", jobs.Count);
        return jobs;
    }

    public async Task<int> RecoverOrphanedSendingJobsAsync()
    {
        _logger.LogInformation("Recovering orphaned jobs in Sending status");

        using var connection = new SqliteConnection(_options.ConnectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Find all jobs in Sending status (orphaned by crash/restart)
            using var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = @"
                SELECT job_id, attempt_count
                FROM jobs
                WHERE status = @sendingStatus;";
            selectCommand.Parameters.AddWithValue("@sendingStatus", JobStatus.Sending.ToString());

            var orphanedJobs = new List<(string JobId, int AttemptCount)>();
            using var reader = await selectCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orphanedJobs.Add((reader.GetString(0), reader.GetInt32(1)));
            }

            if (orphanedJobs.Count == 0)
            {
                _logger.LogDebug("No orphaned Sending jobs found");
                await transaction.CommitAsync();
                return 0;
            }

            // Reset them to RetryScheduled with immediate next attempt
            var now = DateTime.UtcNow;
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = @"
                UPDATE jobs
                SET
                    status = @retryStatus,
                    next_attempt_at_utc = @nextAttempt,
                    last_error = @error
                WHERE status = @sendingStatus;";

            updateCommand.Parameters.AddWithValue("@retryStatus", JobStatus.RetryScheduled.ToString());
            updateCommand.Parameters.AddWithValue("@nextAttempt", now.ToString("o"));
            updateCommand.Parameters.AddWithValue("@error", "Recovered from service restart/crash");
            updateCommand.Parameters.AddWithValue("@sendingStatus", JobStatus.Sending.ToString());

            int recoveredCount = await updateCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();

            _logger.LogWarning(
                "Recovered {RecoveredCount} orphaned Sending jobs. They will be retried immediately.",
                recoveredCount);

            return recoveredCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover orphaned Sending jobs");
            await transaction.RollbackAsync();
            throw;
        }
    }

    private Job ReadJob(SqliteDataReader reader)
    {
        return new Job
        {
            JobId = reader.GetString(0),
            SourcePath = reader.GetString(1),
            ProcessingPath = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = Enum.Parse<JobStatus>(reader.GetString(3)),
            DiscoveredAtUtc = DateTime.Parse(reader.GetString(4)),
            ClaimedAtUtc = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
            SentAtUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            AttemptCount = reader.GetInt32(7),
            NextAttemptAtUtc = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
            LastError = reader.IsDBNull(9) ? null : reader.GetString(9),
            LastHttpStatus = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            FileSizeBytes = reader.IsDBNull(11) ? null : reader.GetInt64(11),
            FileMtimeUtc = reader.IsDBNull(12) ? null : DateTime.Parse(reader.GetString(12))
        };
    }

    /// <summary>
    /// Executes a database operation with retry logic for SQLite busy/locked errors
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        const int maxRetries = 5;
        const int baseDelayMs = 100;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) // SQLITE_BUSY (5) or SQLITE_LOCKED (6)
            {
                if (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex,
                        "Database locked after {MaxRetries} attempts for operation: {Operation}",
                        maxRetries, operationName);
                    throw;
                }

                var delayMs = baseDelayMs * (int)Math.Pow(2, attempt); // Exponential backoff: 100, 200, 400, 800ms
                _logger.LogWarning(
                    "Database locked (attempt {Attempt}/{MaxRetries}), retrying {Operation} in {DelayMs}ms",
                    attempt + 1, maxRetries, operationName, delayMs);

                await Task.Delay(delayMs);
            }
        }

        throw new InvalidOperationException($"Should not reach here - {operationName}");
    }
}

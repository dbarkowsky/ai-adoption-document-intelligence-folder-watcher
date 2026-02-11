using FolderToApi.Service.Configuration;
using FolderToApi.Service.Data;
using FolderToApi.Service.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FolderToApi.Service.Services;

/// <summary>
/// Background service that continuously queries and sends due jobs with bounded concurrency
/// </summary>
public class SendLoopHostedService : BackgroundService
{
    private readonly FolderWatcherOptions _options;
    private readonly IJobRepository _jobRepository;
    private readonly IApiClient _apiClient;
    private readonly IRetryPolicy _retryPolicy;
    private readonly IClock _clock;
    private readonly IFolderStructureService _folderStructure;
    private readonly IStartupInitializationState _startupState;
    private readonly ILogger<SendLoopHostedService> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public SendLoopHostedService(
        IOptions<FolderWatcherOptions> options,
        IJobRepository jobRepository,
        IApiClient apiClient,
        IRetryPolicy retryPolicy,
        IClock clock,
        IFolderStructureService folderStructure,
        IStartupInitializationState startupState,
        ILogger<SendLoopHostedService> logger)
    {
        _options = options.Value;
        _jobRepository = jobRepository;
        _apiClient = apiClient;
        _retryPolicy = retryPolicy;
        _clock = clock;
        _folderStructure = folderStructure;
        _startupState = startupState;
        _logger = logger;
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentSends);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Send loop waiting for startup initialization...");
        var initialized = await _startupState.WaitForInitializationAsync(stoppingToken);
        if (!initialized)
        {
            _logger.LogCritical("Send loop not starting because startup initialization failed.");
            return;
        }

        _logger.LogInformation(
            "SendLoopHostedService started with MaxConcurrentSends={MaxConcurrentSends}",
            _options.MaxConcurrentSends);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueJobsAsync(stoppingToken);

                    // Brief delay before next query
                    await Task.Delay(TimeSpan.FromSeconds(_options.SendLoopIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in send loop. Will retry after delay.");

                    // Longer delay after error
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            await GracefulShutdownAsync();
        }

        _logger.LogInformation("SendLoopHostedService stopped");
    }

    private async Task ProcessDueJobsAsync(CancellationToken stoppingToken)
    {
        var now = _clock.UtcNow;
        var dueJobs = await _jobRepository.GetDueJobsAsync(now);

        if (dueJobs.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Found {Count} due jobs to process", dueJobs.Count);

        // Process jobs with bounded concurrency
        var tasks = dueJobs.Select(job => ProcessJobAsync(job, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessJobAsync(Job job, CancellationToken stoppingToken)
    {
        // Wait for semaphore (bounded concurrency)
        await _concurrencySemaphore.WaitAsync(stoppingToken);

        try
        {
            await SendJobAsync(job, stoppingToken);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task SendJobAsync(Job job, CancellationToken stoppingToken)
    {
        var startTime = _clock.UtcNow;

        try
        {
            _logger.LogInformation(
                "Attempting to send job {JobId}, file {FileName}, attempt {AttemptCount}",
                job.JobId, Path.GetFileName(job.ProcessingPath), job.AttemptCount + 1);

            // Check if file exists
            if (string.IsNullOrEmpty(job.ProcessingPath) || !File.Exists(job.ProcessingPath))
            {
                _logger.LogError(
                    "Job {JobId} file is missing from processing folder: {Path}",
                    job.JobId, job.ProcessingPath);

                await _jobRepository.MarkJobFailedAsync(
                    job.JobId,
                    "File missing from processing folder",
                    null);

                return;
            }

            // Update job to Sending status and increment attempt count
            await _jobRepository.MarkJobSendingAsync(job.JobId);

            // Attempt to send the file
            var result = await _apiClient.UploadFileAsync(job, job.ProcessingPath);

            var elapsed = _clock.UtcNow - startTime;

            if (result.Success)
            {
                await HandleSuccessAsync(job, result, elapsed);
            }
            else
            {
                await HandleFailureAsync(job, result, elapsed);
            }
        }
        catch (Exception ex)
        {
            var elapsed = _clock.UtcNow - startTime;
            _logger.LogError(
                ex,
                "Unexpected error sending job {JobId}: {Error}",
                job.JobId, ex.Message);

            await HandleExceptionAsync(job, ex, elapsed);
        }
    }

    private async Task HandleSuccessAsync(Job job, ApiUploadResult result, TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Job {JobId} sent successfully. HTTP {StatusCode}, {ElapsedMs}ms",
            job.JobId, result.HttpStatusCode, elapsed.TotalMilliseconds);

        // Mark job as sent
        var sentAt = _clock.UtcNow;
        await _jobRepository.MarkJobSentAsync(job.JobId, sentAt, result.HttpStatusCode);

        // Move file to sent folder
        await MoveFileToSentAsync(job);
    }

    private async Task HandleFailureAsync(Job job, ApiUploadResult result, TimeSpan elapsed)
    {
        _logger.LogWarning(
            "Job {JobId} send failed. HTTP {StatusCode}, IsRetryable={IsRetryable}, Error={Error}, {ElapsedMs}ms",
            job.JobId, result.HttpStatusCode, result.IsRetryable, result.ErrorMessage, elapsed.TotalMilliseconds);

        // Reload job to get updated attempt count
        var updatedJob = await _jobRepository.FindJobByIdAsync(job.JobId);
        if (updatedJob == null)
        {
            _logger.LogError("Job {JobId} not found after send attempt", job.JobId);
            return;
        }

        // Evaluate retry policy
        var retryDecision = _retryPolicy.EvaluateRetry(
            updatedJob,
            result.HttpStatusCode,
            result.ErrorMessage != null ? new Exception(result.ErrorMessage) : null);

        if (retryDecision.ShouldRetry)
        {
            // Schedule retry
            _logger.LogInformation(
                "Job {JobId} will retry at {NextAttempt}. Reason: {Reason}",
                updatedJob.JobId, retryDecision.NextAttemptAtUtc, retryDecision.Reason);

            await _jobRepository.RecordRetryAsync(
                updatedJob.JobId,
                result.ErrorMessage ?? "Unknown error",
                result.HttpStatusCode,
                retryDecision.NextAttemptAtUtc!.Value);
        }
        else
        {
            // Dead letter (permanent failure)
            await HandleDeadLetterAsync(updatedJob, result);
        }
    }

    private async Task HandleExceptionAsync(Job job, Exception exception, TimeSpan elapsed)
    {
        _logger.LogError(
            exception,
            "Job {JobId} threw exception during send. {ElapsedMs}ms",
            job.JobId, elapsed.TotalMilliseconds);

        // Reload job to get updated attempt count
        var updatedJob = await _jobRepository.FindJobByIdAsync(job.JobId);
        if (updatedJob == null)
        {
            _logger.LogError("Job {JobId} not found after exception", job.JobId);
            return;
        }

        // Evaluate retry policy
        var retryDecision = _retryPolicy.EvaluateRetry(updatedJob, null, exception);

        if (retryDecision.ShouldRetry)
        {
            // Schedule retry
            _logger.LogInformation(
                "Job {JobId} will retry at {NextAttempt} after exception. Reason: {Reason}",
                updatedJob.JobId, retryDecision.NextAttemptAtUtc, retryDecision.Reason);

            await _jobRepository.RecordRetryAsync(
                updatedJob.JobId,
                exception.Message,
                null,
                retryDecision.NextAttemptAtUtc!.Value);
        }
        else
        {
            // Dead letter (permanent failure)
            await HandleDeadLetterAsync(
                updatedJob,
                ApiUploadResult.PermanentError(exception.Message));
        }
    }

    private async Task HandleDeadLetterAsync(Job job, ApiUploadResult result)
    {
        _logger.LogError(
            "Job {JobId} moving to dead-letter (failed). HTTP {StatusCode}, Error={Error}",
            job.JobId, result.HttpStatusCode, result.ErrorMessage);

        // Mark job as failed
        await _jobRepository.MarkJobFailedAsync(
            job.JobId,
            result.ErrorMessage ?? "Unknown error",
            result.HttpStatusCode);

        // Move file to failed folder
        await MoveFileToFailedAsync(job);
    }

    private async Task MoveFileToSentAsync(Job job)
    {
        try
        {
            if (string.IsNullOrEmpty(job.ProcessingPath) || !File.Exists(job.ProcessingPath))
            {
                _logger.LogWarning(
                    "Job {JobId} file not found when moving to sent: {Path}",
                    job.JobId, job.ProcessingPath);
                return;
            }

            var fileName = Path.GetFileName(job.ProcessingPath);
            var sentPath = Path.Combine(_folderStructure.SentPath, fileName);

            // Handle duplicate names
            sentPath = GetUniqueFilePath(sentPath);

            File.Move(job.ProcessingPath, sentPath);

            // Also remove the job-specific processing folder if empty
            var processingFolder = Path.GetDirectoryName(job.ProcessingPath);
            if (!string.IsNullOrEmpty(processingFolder) && Directory.Exists(processingFolder))
            {
                if (!Directory.EnumerateFileSystemEntries(processingFolder).Any())
                {
                    Directory.Delete(processingFolder);
                }
            }

            _logger.LogDebug("Moved job {JobId} file to sent: {SentPath}", job.JobId, sentPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move job {JobId} file to sent folder: {Error}",
                job.JobId, ex.Message);
        }
    }

    private async Task MoveFileToFailedAsync(Job job)
    {
        try
        {
            if (string.IsNullOrEmpty(job.ProcessingPath) || !File.Exists(job.ProcessingPath))
            {
                _logger.LogWarning(
                    "Job {JobId} file not found when moving to failed: {Path}",
                    job.JobId, job.ProcessingPath);
                return;
            }

            // Prefix filename with jobId for easy correlation with database records
            var originalFileName = Path.GetFileName(job.ProcessingPath);
            var fileName = $"{job.JobId}-{originalFileName}";
            var failedPath = Path.Combine(_folderStructure.FailedPath, fileName);

            // Handle duplicate names
            failedPath = GetUniqueFilePath(failedPath);

            File.Move(job.ProcessingPath, failedPath);

            // Also remove the job-specific processing folder if empty
            var processingFolder = Path.GetDirectoryName(job.ProcessingPath);
            if (!string.IsNullOrEmpty(processingFolder) && Directory.Exists(processingFolder))
            {
                if (!Directory.EnumerateFileSystemEntries(processingFolder).Any())
                {
                    Directory.Delete(processingFolder);
                }
            }

            _logger.LogDebug("Moved job {JobId} file to failed: {FailedPath}", job.JobId, failedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to move job {JobId} file to failed folder: {Error}",
                job.JobId, ex.Message);
        }
    }

    private string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExtension}_{counter}{extension}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }

    private async Task GracefulShutdownAsync()
    {
        _logger.LogInformation("SendLoopHostedService shutting down gracefully...");

        // Wait for all in-flight sends to complete (with timeout)
        var timeout = TimeSpan.FromSeconds(30);
        var shutdownStart = DateTime.UtcNow;

        while (_concurrencySemaphore.CurrentCount < _options.MaxConcurrentSends)
        {
            if (DateTime.UtcNow - shutdownStart > timeout)
            {
                _logger.LogWarning(
                    "Shutdown timeout exceeded. {InFlight} sends may not have completed.",
                    _options.MaxConcurrentSends - _concurrencySemaphore.CurrentCount);
                break;
            }

            await Task.Delay(100);
        }

        _concurrencySemaphore.Dispose();
        _logger.LogInformation("SendLoopHostedService shutdown complete");
    }
}

using FolderToApi.Service.Configuration;
using FolderToApi.Service.Data;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Hosted service that periodically scans the inbox for new files and claims them
/// </summary>
public class ScanLoopHostedService : BackgroundService
{
    private readonly FolderWatcherOptions _options;
    private readonly IInboxEnumerator _enumerator;
    private readonly ICompletenessChecker _completenessChecker;
    private readonly IFileClaimer _fileClaimer;
    private readonly IJobRepository _jobRepository;
    private readonly IStartupInitializationState _startupState;
    private readonly ILogger<ScanLoopHostedService> _logger;
    private readonly TimeSpan _scanInterval;

    public ScanLoopHostedService(
        IOptions<FolderWatcherOptions> options,
        IInboxEnumerator enumerator,
        ICompletenessChecker completenessChecker,
        IFileClaimer fileClaimer,
        IJobRepository jobRepository,
        IStartupInitializationState startupState,
        ILogger<ScanLoopHostedService> logger)
    {
        _options = options.Value;
        _enumerator = enumerator;
        _completenessChecker = completenessChecker;
        _fileClaimer = fileClaimer;
        _jobRepository = jobRepository;
        _startupState = startupState;
        _logger = logger;
        _scanInterval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan loop waiting for startup initialization...");
        var initialized = await _startupState.WaitForInitializationAsync(stoppingToken);
        if (!initialized)
        {
            _logger.LogCritical("Scan loop not starting because startup initialization failed.");
            return;
        }

        _logger.LogInformation("Scan loop starting with interval: {Interval} seconds", _options.ScanIntervalSeconds);

        // Wait a bit before starting first scan to let the system settle
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        using var timer = new PeriodicTimer(_scanInterval);

        try
        {
            // Run first scan immediately
            await RunScanAsync(stoppingToken);

            // Then run on schedule
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunScanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in scan loop");
            throw;
        }

        _logger.LogInformation("Scan loop stopped");
    }

    private async Task RunScanAsync(CancellationToken cancellationToken)
    {
        var scanStartTime = DateTime.UtcNow;
        _logger.LogInformation("Starting inbox scan");

        try
        {
            int filesScanned = 0;
            int filesSkippedLocked = 0;
            int filesSkippedTooNew = 0;
            int filesSkippedGone = 0;
            int filesSkippedAlreadyClaimed = 0;
            int filesClaimedNew = 0;
            int filesClaimFailed = 0;

            // Step 1: Enumerate eligible files
            var eligibleFiles = _enumerator.EnumerateEligibleFiles().ToList();

            if (eligibleFiles.Count == 0)
            {
                _logger.LogDebug("No eligible files found in inbox");
                return;
            }

            _logger.LogInformation("Found {Count} eligible files to process", eligibleFiles.Count);

            // Step 2: Process each file
            foreach (var file in eligibleFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Scan cancelled mid-processing");
                    break;
                }

                filesScanned++;

                try
                {
                    var result = await ProcessFileAsync(file);

                    switch (result)
                    {
                        case ProcessFileResult.Claimed:
                            filesClaimedNew++;
                            break;
                        case ProcessFileResult.Locked:
                            filesSkippedLocked++;
                            break;
                        case ProcessFileResult.TooNew:
                            filesSkippedTooNew++;
                            break;
                        case ProcessFileResult.Gone:
                            filesSkippedGone++;
                            break;
                        case ProcessFileResult.AlreadyClaimed:
                            filesSkippedAlreadyClaimed++;
                            break;
                        case ProcessFileResult.Failed:
                            filesClaimFailed++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file: {FilePath}", file.FullName);
                    filesClaimFailed++;
                }
            }

            var scanDuration = DateTime.UtcNow - scanStartTime;

            _logger.LogInformation(
                "Scan complete in {Duration:F1}s: Scanned={Scanned}, Claimed={Claimed}, " +
                "Locked={Locked}, TooNew={TooNew}, AlreadyClaimed={AlreadyClaimed}, Failed={Failed}",
                scanDuration.TotalSeconds,
                filesScanned,
                filesClaimedNew,
                filesSkippedLocked,
                filesSkippedTooNew,
                filesSkippedAlreadyClaimed,
                filesClaimFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during inbox scan");
            // Don't rethrow - let the scan loop continue
        }
    }

    private async Task<ProcessFileResult> ProcessFileAsync(IFileInfo file)
    {
        // Step 1: Check completeness
        var completenessResult = _completenessChecker.CheckFile(file);

        switch (completenessResult.Status)
        {
            case FileCompletenessStatus.Locked:
                _logger.LogDebug("File locked, skipping: {FileName}", file.Name);
                return ProcessFileResult.Locked;

            case FileCompletenessStatus.TooNew:
                _logger.LogDebug("File too new, skipping: {FileName}", file.Name);
                return ProcessFileResult.TooNew;

            case FileCompletenessStatus.Gone:
                _logger.LogDebug("File disappeared, skipping: {FileName}", file.Name);
                return ProcessFileResult.Gone;

            case FileCompletenessStatus.Error:
                _logger.LogWarning("Error checking file completeness: {FileName}, Error: {Error}",
                    file.Name, completenessResult.ErrorMessage);
                return ProcessFileResult.Failed;

            case FileCompletenessStatus.Complete:
                // Continue processing
                break;

            default:
                _logger.LogWarning("Unknown completeness status: {Status}", completenessResult.Status);
                return ProcessFileResult.Failed;
        }

        // Step 2: Check if already claimed (determinism check)
        var existingJob = await _jobRepository.FindJobBySourcePathAsync(file.FullName);

        if (existingJob != null)
        {
            _logger.LogDebug("File already has a job (ID={JobId}), skipping: {FileName}",
                existingJob.JobId, file.Name);
            return ProcessFileResult.AlreadyClaimed;
        }

        // Step 3: Claim the file
        var jobId = Guid.NewGuid().ToString();
        var claimResult = await _fileClaimer.ClaimFileAsync(file, jobId);

        switch (claimResult.Status)
        {
            case ClaimStatus.Success:
                // Step 4: Create job in database
                try
                {
                    await _jobRepository.CreateJobAsync(
                        jobId,
                        file.FullName,
                        file.Length,
                        file.LastWriteTimeUtc);

                    await _jobRepository.UpdateJobClaimedAsync(
                        jobId,
                        claimResult.ProcessingPath!,
                        DateTime.UtcNow);

                    _logger.LogInformation(
                        "File claimed successfully: {FileName} → Job {JobId} (CrossVolume={CrossVolume})",
                        file.Name,
                        jobId,
                        claimResult.UsedCrossVolumeFallback);

                    return ProcessFileResult.Claimed;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create job in database for claimed file: {FileName}", file.Name);
                    return ProcessFileResult.Failed;
                }

            case ClaimStatus.AlreadyClaimed:
                _logger.LogDebug("File already claimed: {FileName}", file.Name);
                return ProcessFileResult.AlreadyClaimed;

            case ClaimStatus.TransientFailure:
                _logger.LogWarning("Transient claim failure for {FileName}: {Error}",
                    file.Name, claimResult.ErrorMessage);
                return ProcessFileResult.Failed;

            case ClaimStatus.PermanentFailure:
                _logger.LogError("Permanent claim failure for {FileName}: {Error}",
                    file.Name, claimResult.ErrorMessage);
                return ProcessFileResult.Failed;

            default:
                return ProcessFileResult.Failed;
        }
    }

    private enum ProcessFileResult
    {
        Claimed,
        Locked,
        TooNew,
        Gone,
        AlreadyClaimed,
        Failed
    }
}

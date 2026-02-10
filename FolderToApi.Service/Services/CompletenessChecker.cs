using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Checks file completeness using exclusive open (FileShare.None)
/// </summary>
public class CompletenessChecker : ICompletenessChecker
{
    private readonly FolderWatcherOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CompletenessChecker> _logger;
    private readonly TimeSpan _stableAge;

    public CompletenessChecker(
        IOptions<FolderWatcherOptions> options,
        IFileSystem fileSystem,
        ILogger<CompletenessChecker> logger)
    {
        _options = options.Value;
        _fileSystem = fileSystem;
        _logger = logger;
        _stableAge = TimeSpan.FromSeconds(_options.StableAgeSeconds);

        _logger.LogInformation("CompletenessChecker initialized with StableAgeSeconds={StableAge}",
            _options.StableAgeSeconds);
    }

    public CompletenessCheckResult CheckFile(IFileInfo file)
    {
        var result = new CompletenessCheckResult
        {
            CheckedAtUtc = DateTime.UtcNow
        };

        try
        {
            // Refresh file info to get current state
            file.Refresh();

            // Check 1: File still exists
            if (!file.Exists)
            {
                _logger.LogDebug("File disappeared: {FilePath}", file.FullName);
                result.Status = FileCompletenessStatus.Gone;
                result.ErrorMessage = "File no longer exists";
                return result;
            }

            // Check 2: Stability age heuristic
            var fileAge = DateTime.UtcNow - file.LastWriteTimeUtc;
            if (fileAge < _stableAge)
            {
                _logger.LogDebug(
                    "File too new: {FileName}, age={Age:F1}s, required={Required}s",
                    file.Name,
                    fileAge.TotalSeconds,
                    _stableAge.TotalSeconds);

                result.Status = FileCompletenessStatus.TooNew;
                result.ErrorMessage = $"File age ({fileAge.TotalSeconds:F1}s) is less than required stability age ({_stableAge.TotalSeconds}s)";
                return result;
            }

            // Check 3: Exclusive open test
            if (!TryExclusiveOpen(file, out string? errorMessage))
            {
                result.Status = FileCompletenessStatus.Locked;
                result.ErrorMessage = errorMessage;
                return result;
            }

            // File passed all checks
            _logger.LogDebug("File is complete and ready: {FileName}", file.Name);
            result.Status = FileCompletenessStatus.Complete;
            return result;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogDebug(ex, "File not found during completeness check: {FilePath}", file.FullName);
            result.Status = FileCompletenessStatus.Gone;
            result.ErrorMessage = "File not found";
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied during completeness check: {FilePath}", file.FullName);
            result.Status = FileCompletenessStatus.Error;
            result.ErrorMessage = $"Access denied: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during completeness check: {FilePath}", file.FullName);
            result.Status = FileCompletenessStatus.Error;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            return result;
        }
    }

    private bool TryExclusiveOpen(IFileInfo file, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            // Attempt to open the file with exclusive access (no sharing)
            using var stream = _fileSystem.File.Open(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            // Success! File is not locked
            _logger.LogDebug("Exclusive open succeeded for: {FileName}", file.Name);
            return true;
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            // File is locked by another process
            _logger.LogDebug("File is locked: {FileName}", file.Name);
            errorMessage = "File is locked by another process";
            return false;
        }
        catch (IOException ex)
        {
            // Other I/O error
            _logger.LogWarning(ex, "I/O error opening file: {FilePath}", file.FullName);
            errorMessage = $"I/O error: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied opening file: {FilePath}", file.FullName);
            errorMessage = $"Access denied: {ex.Message}";
            return false;
        }
    }

    private bool IsSharingViolation(IOException ex)
    {
        // Check for ERROR_SHARING_VIOLATION (32) or ERROR_LOCK_VIOLATION (33)
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;

        int errorCode = ex.HResult & 0xFFFF;
        return errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION;
    }
}

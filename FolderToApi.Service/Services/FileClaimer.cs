using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Claims files by moving them from inbox to processing with cross-volume support
/// </summary>
public class FileClaimer : IFileClaimer
{
    private readonly IFolderStructureService _folderStructure;
    private readonly IFileSystem _fileSystem;
    private readonly FolderWatcherOptions _options;
    private readonly ILogger<FileClaimer> _logger;

    public FileClaimer(
        IFolderStructureService folderStructure,
        IFileSystem fileSystem,
        IOptions<FolderWatcherOptions> options,
        ILogger<FileClaimer> logger)
    {
        _folderStructure = folderStructure;
        _fileSystem = fileSystem;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClaimResult> ClaimFileAsync(IFileInfo sourceFile, string jobId)
    {
        var result = new ClaimResult();

        try
        {
            // Validate path length before processing
            if (sourceFile.FullName.Length > _options.MaxPathLength)
            {
                _logger.LogError(
                    "Path too long for file: {FileName} ({Length} characters exceeds limit of {MaxLength})",
                    sourceFile.Name,
                    sourceFile.FullName.Length,
                    _options.MaxPathLength);
                result.Status = ClaimStatus.PermanentFailure;
                result.ErrorMessage = $"Path too long ({sourceFile.FullName.Length} > {_options.MaxPathLength} characters)";
                return result;
            }

            // Create processing subdirectory for this job
            var processingDir = Path.Combine(_folderStructure.ProcessingPath, jobId);
            _fileSystem.Directory.CreateDirectory(processingDir);

            // Destination path
            var destinationPath = Path.Combine(processingDir, sourceFile.Name);

            // Check for filename conflicts and resolve by adding GUID suffix
            if (_fileSystem.File.Exists(destinationPath))
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFile.Name);
                var extension = Path.GetExtension(sourceFile.Name);
                var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                var uniqueFileName = $"{fileNameWithoutExt}_{uniqueSuffix}{extension}";
                destinationPath = Path.Combine(processingDir, uniqueFileName);

                _logger.LogWarning(
                    "Filename conflict detected, using unique name: {OriginalName} → {UniqueName}",
                    sourceFile.Name,
                    uniqueFileName);
            }

            result.ProcessingPath = destinationPath;

            _logger.LogDebug("Claiming file: {SourcePath} → {DestinationPath}",
                sourceFile.FullName, destinationPath);

            // Attempt same-volume move first
            bool moveSucceeded = await TryAtomicMoveAsync(sourceFile.FullName, destinationPath);

            if (moveSucceeded)
            {
                _logger.LogInformation("File claimed successfully: {FileName} → {JobId}",
                    sourceFile.Name, jobId);
                result.Status = ClaimStatus.Success;
                result.UsedCrossVolumeFallback = false;
                return result;
            }

            // Fall back to copy-verify-delete for cross-volume scenarios
            _logger.LogDebug("Atomic move failed, using cross-volume fallback for: {FileName}",
                sourceFile.Name);

            bool copySucceeded = await TryCopyVerifyDeleteAsync(sourceFile, destinationPath);

            if (copySucceeded)
            {
                _logger.LogInformation(
                    "File claimed via cross-volume fallback: {FileName} → {JobId}",
                    sourceFile.Name, jobId);
                result.Status = ClaimStatus.Success;
                result.UsedCrossVolumeFallback = true;
                return result;
            }

            // Both methods failed
            result.Status = ClaimStatus.TransientFailure;
            result.ErrorMessage = "Failed to claim file with both move and copy methods";
            return result;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogDebug(ex, "File already claimed or disappeared: {FilePath}", sourceFile.FullName);
            result.Status = ClaimStatus.AlreadyClaimed;
            result.ErrorMessage = "File not found (likely already claimed)";
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied claiming file: {FilePath}", sourceFile.FullName);
            result.Status = ClaimStatus.PermanentFailure;
            result.ErrorMessage = $"Access denied: {ex.Message}";
            return result;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error claiming file: {FilePath}", sourceFile.FullName);
            result.Status = ClaimStatus.TransientFailure;
            result.ErrorMessage = $"I/O error: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error claiming file: {FilePath}", sourceFile.FullName);
            result.Status = ClaimStatus.PermanentFailure;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            return result;
        }
    }

    private async Task<bool> TryAtomicMoveAsync(string sourcePath, string destinationPath)
    {
        try
        {
            // Attempt atomic move
            _fileSystem.File.Move(sourcePath, destinationPath, overwrite: false);
            _logger.LogDebug("Atomic move succeeded: {FileName}", Path.GetFileName(sourcePath));
            return true;
        }
        catch (IOException ex)
        {
            // This could be cross-volume or other I/O issue
            _logger.LogDebug(ex, "Atomic move failed (likely cross-volume): {FileName}",
                Path.GetFileName(sourcePath));
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied during move: {SourcePath}", sourcePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error during move: {SourcePath}", sourcePath);
            return false;
        }
    }

    private async Task<bool> TryCopyVerifyDeleteAsync(IFileInfo sourceFile, string destinationPath)
    {
        string? tempDestinationPath = null;

        try
        {
            // Step 1: Copy to temporary name
            tempDestinationPath = destinationPath + ".tmp";

            _logger.LogDebug("Copying to temp: {Source} → {TempDest}",
                sourceFile.FullName, tempDestinationPath);

            await CopyFileAsync(sourceFile.FullName, tempDestinationPath);

            // Step 2: Verify integrity (size check)
            var destFileInfo = _fileSystem.FileInfo.New(tempDestinationPath);
            if (!destFileInfo.Exists)
            {
                _logger.LogError("Destination file not found after copy: {Path}", tempDestinationPath);
                return false;
            }

            if (destFileInfo.Length != sourceFile.Length)
            {
                _logger.LogError(
                    "File size mismatch after copy: source={SourceSize}, dest={DestSize}",
                    sourceFile.Length, destFileInfo.Length);

                // Clean up bad copy
                _fileSystem.File.Delete(tempDestinationPath);
                return false;
            }

            _logger.LogDebug("Copy verified (size: {Size} bytes)", destFileInfo.Length);

            // Step 3: Delete source
            _fileSystem.File.Delete(sourceFile.FullName);
            _logger.LogDebug("Source file deleted: {SourcePath}", sourceFile.FullName);

            // Step 4: Rename temp to final name
            _fileSystem.File.Move(tempDestinationPath, destinationPath, overwrite: false);
            _logger.LogDebug("Temp file renamed to final: {DestinationPath}", destinationPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during copy-verify-delete: {SourcePath}", sourceFile.FullName);

            // Clean up temp file if it exists
            if (tempDestinationPath != null && _fileSystem.File.Exists(tempDestinationPath))
            {
                try
                {
                    _fileSystem.File.Delete(tempDestinationPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up temp file: {Path}", tempDestinationPath);
                }
            }

            return false;
        }
    }

    private async Task CopyFileAsync(string sourcePath, string destinationPath)
    {
        const int bufferSize = 81920; // 80 KB buffer

        using var sourceStream = _fileSystem.FileStream.New(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize,
            useAsync: true);

        using var destinationStream = _fileSystem.FileStream.New(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true);

        await sourceStream.CopyToAsync(destinationStream);
        await destinationStream.FlushAsync();
    }
}

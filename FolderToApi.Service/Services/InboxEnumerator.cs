using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace FolderToApi.Service.Services;

/// <summary>
/// Enumerates files in the inbox and applies eligibility filters
/// </summary>
public class InboxEnumerator : IInboxEnumerator
{
    private readonly FolderWatcherOptions _options;
    private readonly IFolderStructureService _folderStructure;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<InboxEnumerator> _logger;
    private readonly long _maxFileSizeBytes;
    private readonly HashSet<string> _allowedExtensionsLowerCase;
    private readonly List<string> _ignorePatterns;

    public InboxEnumerator(
        IOptions<FolderWatcherOptions> options,
        IFolderStructureService folderStructure,
        IFileSystem fileSystem,
        ILogger<InboxEnumerator> logger)
    {
        _options = options.Value;
        _folderStructure = folderStructure;
        _fileSystem = fileSystem;
        _logger = logger;

        _maxFileSizeBytes = _options.MaxFileSizeMB * 1024L * 1024L;

        // Normalize extensions to lowercase for case-insensitive comparison
        _allowedExtensionsLowerCase = new HashSet<string>(
            _options.AllowedExtensions.Select(ext => ext.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase
        );

        // Default ignore patterns: temp files, hidden files, system files
        _ignorePatterns = new List<string> { "~*", ".*", "*.tmp", "*.temp" };

        _logger.LogInformation(
            "InboxEnumerator initialized: AllowedExtensions={Extensions}, MaxSizeMB={MaxSize}, Recursive={Recursive}",
            string.Join(", ", _options.AllowedExtensions),
            _options.MaxFileSizeMB,
            _options.RecursiveScan);
    }

    public IEnumerable<IFileInfo> EnumerateEligibleFiles()
    {
        _logger.LogDebug("Starting inbox enumeration: {InboxPath}", _folderStructure.InboxPath);

        var eligibleFiles = new List<IFileInfo>();

        // Enumerate files in the inbox
        var searchOption = _options.RecursiveScan
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var directoryInfo = _fileSystem.DirectoryInfo.New(_folderStructure.InboxPath);

        if (!directoryInfo.Exists)
        {
            _logger.LogWarning("Inbox directory does not exist: {InboxPath}", _folderStructure.InboxPath);
            return eligibleFiles;
        }

        IEnumerable<IFileInfo> files;

        try
        {
            files = directoryInfo.EnumerateFiles("*", searchOption);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Inbox directory not found: {InboxPath}", _folderStructure.InboxPath);
            return eligibleFiles;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to inbox directory: {InboxPath}", _folderStructure.InboxPath);
            return eligibleFiles;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error accessing inbox directory: {InboxPath}", _folderStructure.InboxPath);
            return eligibleFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error enumerating inbox: {InboxPath}", _folderStructure.InboxPath);
            return eligibleFiles;
        }

        int totalCount = 0;

        // Apply filters and collect eligible files
        foreach (var file in files)
        {
            totalCount++;

            try
            {
                // Filter 1: Check if file is accessible
                if (!IsFileAccessible(file))
                {
                    _logger.LogDebug("Skipping inaccessible file: {FilePath}", file.FullName);
                    continue;
                }

                // Filter 2: Check system/hidden attributes
                if (IsSystemOrHiddenFile(file))
                {
                    _logger.LogDebug("Skipping system/hidden file: {FilePath}", file.FullName);
                    continue;
                }

                // Filter 3: Check ignore patterns
                if (MatchesIgnorePattern(file.Name))
                {
                    _logger.LogDebug("Skipping file matching ignore pattern: {FileName}", file.Name);
                    continue;
                }

                // Filter 4: Check allowed extensions
                if (!HasAllowedExtension(file))
                {
                    _logger.LogDebug("Skipping file with disallowed extension: {FileName}", file.Name);
                    continue;
                }

                // Filter 5: Check file size
                if (IsOversized(file))
                {
                    _logger.LogWarning(
                        "Skipping oversized file: {FileName} ({SizeMB:F2} MB exceeds limit of {MaxMB} MB)",
                        file.Name,
                        file.Length / (1024.0 * 1024.0),
                        _options.MaxFileSizeMB);
                    continue;
                }

                // Filter 6: Check zero-byte files
                if (IsZeroByteFile(file))
                {
                    if (_options.SkipZeroByteFiles)
                    {
                        _logger.LogDebug("Skipping zero-byte file: {FileName}", file.Name);
                        continue;
                    }
                    else
                    {
                        _logger.LogDebug("Processing zero-byte file (SkipZeroByteFiles=false): {FileName}", file.Name);
                    }
                }

                // File passed all filters
                _logger.LogDebug("Eligible file found: {FileName} ({SizeKB:F2} KB)",
                    file.Name,
                    file.Length / 1024.0);

                eligibleFiles.Add(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing file: {FilePath}", file.FullName);
                // Continue with next file
            }
        }

        _logger.LogInformation(
            "Inbox enumeration complete: {Eligible} eligible files out of {Total} total files",
            eligibleFiles.Count,
            totalCount);

        return eligibleFiles;
    }

    private bool IsFileAccessible(IFileInfo file)
    {
        try
        {
            // Refresh file info to get current attributes
            file.Refresh();
            return file.Exists;
        }
        catch
        {
            return false;
        }
    }

    private bool IsSystemOrHiddenFile(IFileInfo file)
    {
        try
        {
            var attributes = file.Attributes;
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                   (attributes & FileAttributes.System) == FileAttributes.System;
        }
        catch
        {
            // If we can't read attributes, consider it inaccessible
            return true;
        }
    }

    private bool MatchesIgnorePattern(string fileName)
    {
        foreach (var pattern in _ignorePatterns)
        {
            // Convert simple wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAllowedExtension(IFileInfo file)
    {
        if (_allowedExtensionsLowerCase.Count == 0)
        {
            // If no extensions configured, allow all
            return true;
        }

        var extension = file.Extension.ToLowerInvariant();
        return _allowedExtensionsLowerCase.Contains(extension);
    }

    private bool IsOversized(IFileInfo file)
    {
        return file.Length > _maxFileSizeBytes;
    }

    private bool IsZeroByteFile(IFileInfo file)
    {
        return file.Length == 0;
    }
}

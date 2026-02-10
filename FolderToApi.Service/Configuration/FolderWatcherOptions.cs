using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class FolderWatcherOptions
{
    public const string SectionName = "FolderWatcher";

    [Required(ErrorMessage = "RootPath is required")]
    [MinLength(1, ErrorMessage = "RootPath cannot be empty")]
    public string RootPath { get; set; } = string.Empty;

    [Range(10, 120, ErrorMessage = "ScanIntervalSeconds must be between 10 and 120")]
    public int ScanIntervalSeconds { get; set; } = 30;

    [Range(0, 300, ErrorMessage = "StableAgeSeconds must be between 0 and 300")]
    public int StableAgeSeconds { get; set; } = 10;

    [Required(ErrorMessage = "AllowedExtensions is required")]
    [MinLength(1, ErrorMessage = "At least one allowed extension must be specified")]
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

    [Range(1, 1024, ErrorMessage = "MaxFileSizeMB must be between 1 and 1024")]
    public int MaxFileSizeMB { get; set; } = 50;

    public bool RecursiveScan { get; set; } = false;

    /// <summary>
    /// Maximum number of concurrent file uploads to the API
    /// </summary>
    [Range(1, 50, ErrorMessage = "MaxConcurrentSends must be between 1 and 50")]
    public int MaxConcurrentSends { get; set; } = 5;

    /// <summary>
    /// Interval in seconds between send loop iterations when no jobs are due
    /// </summary>
    [Range(1, 60, ErrorMessage = "SendLoopIntervalSeconds must be between 1 and 60")]
    public int SendLoopIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Whether to skip zero-byte files. If true, zero-byte files are filtered out.
    /// If false, zero-byte files are processed normally.
    /// </summary>
    public bool SkipZeroByteFiles { get; set; } = false;

    /// <summary>
    /// Maximum path length in characters. Windows typically supports 260 characters (MAX_PATH).
    /// Paths exceeding this limit will be rejected and moved to failed folder.
    /// </summary>
    [Range(200, 32767, ErrorMessage = "MaxPathLength must be between 200 and 32767")]
    public int MaxPathLength { get; set; } = 260;
}

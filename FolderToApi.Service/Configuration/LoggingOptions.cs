using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class LoggingOptions
{
    public const string SectionName = "Logging";

    /// <summary>
    /// Directory path for log files
    /// </summary>
    [Required(ErrorMessage = "LogDirectory is required")]
    public string LogDirectory { get; set; } = @"C:\Logs\FolderToApi";

    /// <summary>
    /// Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal)
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Whether to write logs to console
    /// </summary>
    public bool WriteToConsole { get; set; } = true;

    /// <summary>
    /// Whether to write logs to file
    /// </summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>
    /// Whether to write to Windows Event Log
    /// </summary>
    public bool WriteToEventLog { get; set; } = true;

    /// <summary>
    /// Rolling interval for log files (Day, Hour, Minute, Infinite)
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Maximum file size in bytes before rolling (0 = no size limit)
    /// </summary>
    [Range(0, long.MaxValue)]
    public long FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Number of log files to retain (null = unlimited)
    /// </summary>
    [Range(1, 365)]
    public int? RetainedFileCountLimit { get; set; } = 30;

    /// <summary>
    /// Whether to use JSON formatting (CompactJsonFormatter)
    /// </summary>
    public bool UseJsonFormatting { get; set; } = true;
}

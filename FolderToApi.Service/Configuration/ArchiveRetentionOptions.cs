using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class ArchiveRetentionOptions
{
    public const string SectionName = "ArchiveRetention";

    [Range(1, 365, ErrorMessage = "SentRetentionDays must be between 1 and 365")]
    public int SentRetentionDays { get; set; } = 30;

    [Range(1, 730, ErrorMessage = "FailedRetentionDays must be between 1 and 730")]
    public int FailedRetentionDays { get; set; } = 90;

    public bool EnableAutoCleanup { get; set; } = true;

    [Range(1, 168, ErrorMessage = "CleanupIntervalHours must be between 1 and 168")]
    public int CleanupIntervalHours { get; set; } = 24;
}

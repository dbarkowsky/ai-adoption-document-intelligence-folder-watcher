using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class RetryPolicyOptions
{
    public const string SectionName = "RetryPolicy";

    [Range(1, 100, ErrorMessage = "MaxAttempts must be between 1 and 100")]
    public int MaxAttempts { get; set; } = 10;

    [Range(1, 365, ErrorMessage = "MaxJobAgeDays must be between 1 and 365")]
    public int MaxJobAgeDays { get; set; } = 7;

    /// <summary>
    /// Optional explicit retry schedule in seconds. If provided, this schedule is used instead of exponential backoff formula.
    /// Example: [10, 30, 120, 300, 900, 1800, 3600] = 10s, 30s, 2m, 5m, 15m, 30m, 60m
    /// </summary>
    public int[]? RetryScheduleSeconds { get; set; }

    [Range(1, 3600, ErrorMessage = "InitialDelaySeconds must be between 1 and 3600")]
    public int InitialDelaySeconds { get; set; } = 10;

    [Range(60, 86400, ErrorMessage = "MaxDelaySeconds must be between 60 and 86400")]
    public int MaxDelaySeconds { get; set; } = 3600;

    [Range(1.1, 10.0, ErrorMessage = "BackoffMultiplier must be between 1.1 and 10")]
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Jitter percentage to add randomness to backoff delays (0.0 = no jitter, 0.2 = ±20% jitter)
    /// </summary>
    [Range(0.0, 0.5, ErrorMessage = "JitterFactor must be between 0 and 0.5")]
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// HTTP status codes that should trigger a retry
    /// </summary>
    public int[] RetryableHttpStatusCodes { get; set; } = { 408, 429, 500, 502, 503, 504 };

    /// <summary>
    /// HTTP status codes that should NOT trigger a retry (permanent failures)
    /// </summary>
    public int[] NonRetryableHttpStatusCodes { get; set; } = { 400, 401, 403, 404, 415 };
}

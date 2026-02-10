namespace FolderToApi.Service.Models;

/// <summary>
/// Represents the decision about whether and when to retry a failed job
/// </summary>
public class RetryDecision
{
    /// <summary>
    /// Whether the job should be retried
    /// </summary>
    public bool ShouldRetry { get; set; }

    /// <summary>
    /// When the next retry attempt should occur (UTC), if ShouldRetry is true
    /// </summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>
    /// Reason for the decision (for logging and debugging)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether the error is considered retryable
    /// </summary>
    public bool IsRetryableError { get; set; }

    /// <summary>
    /// Creates a decision to retry with a specific next attempt time
    /// </summary>
    public static RetryDecision Retry(DateTime nextAttemptAtUtc, string reason)
    {
        return new RetryDecision
        {
            ShouldRetry = true,
            NextAttemptAtUtc = nextAttemptAtUtc,
            Reason = reason,
            IsRetryableError = true
        };
    }

    /// <summary>
    /// Creates a decision to not retry (dead-letter)
    /// </summary>
    public static RetryDecision DeadLetter(string reason)
    {
        return new RetryDecision
        {
            ShouldRetry = false,
            NextAttemptAtUtc = null,
            Reason = reason,
            IsRetryableError = false
        };
    }
}

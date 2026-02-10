using FolderToApi.Service.Models;

namespace FolderToApi.Service.Services;

/// <summary>
/// Determines whether and when a failed job should be retried
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Evaluates a job and determines if it should be retried, and if so, when
    /// </summary>
    /// <param name="job">The job that failed</param>
    /// <param name="httpStatusCode">HTTP status code from the failed attempt, if applicable</param>
    /// <param name="exception">Exception from the failed attempt, if applicable</param>
    /// <returns>A RetryDecision indicating whether to retry and when</returns>
    RetryDecision EvaluateRetry(Job job, int? httpStatusCode = null, Exception? exception = null);

    /// <summary>
    /// Determines if an HTTP status code or exception is retryable
    /// </summary>
    /// <param name="httpStatusCode">HTTP status code, if applicable</param>
    /// <param name="exception">Exception, if applicable</param>
    /// <returns>True if the error is retryable, false for permanent failures</returns>
    bool IsRetryableError(int? httpStatusCode = null, Exception? exception = null);

    /// <summary>
    /// Calculates the next attempt time for a job based on its current attempt count
    /// </summary>
    /// <param name="attemptCount">Current number of attempts (0-based)</param>
    /// <returns>The next attempt time in UTC</returns>
    DateTime CalculateNextAttemptTime(int attemptCount);
}

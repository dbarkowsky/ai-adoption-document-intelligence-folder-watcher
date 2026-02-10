using FolderToApi.Service.Configuration;
using FolderToApi.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FolderToApi.Service.Services;

/// <summary>
/// Implementation of retry policy with exponential backoff, jitter, and configurable limits
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private readonly RetryPolicyOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<RetryPolicy> _logger;
    private readonly Random _random = new Random();

    public RetryPolicy(
        IOptions<RetryPolicyOptions> options,
        IClock clock,
        ILogger<RetryPolicy> logger)
    {
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public RetryDecision EvaluateRetry(Job job, int? httpStatusCode = null, Exception? exception = null)
    {
        var now = _clock.UtcNow;

        // Check if error is retryable
        if (!IsRetryableError(httpStatusCode, exception))
        {
            _logger.LogInformation(
                "Job {JobId} has non-retryable error (HTTP {Status}). Moving to dead-letter.",
                job.JobId, httpStatusCode);
            return RetryDecision.DeadLetter($"Non-retryable error: HTTP {httpStatusCode}");
        }

        // Check max attempts limit
        if (job.AttemptCount >= _options.MaxAttempts)
        {
            _logger.LogWarning(
                "Job {JobId} exceeded MaxAttempts ({MaxAttempts}). Moving to dead-letter.",
                job.JobId, _options.MaxAttempts);
            return RetryDecision.DeadLetter($"Exceeded max attempts ({_options.MaxAttempts})");
        }

        // Check max job age limit
        var jobAge = now - job.DiscoveredAtUtc;
        if (jobAge.TotalDays >= _options.MaxJobAgeDays)
        {
            _logger.LogWarning(
                "Job {JobId} exceeded MaxJobAgeDays ({MaxJobAgeDays} days, actual {ActualDays:F1} days). Moving to dead-letter.",
                job.JobId, _options.MaxJobAgeDays, jobAge.TotalDays);
            return RetryDecision.DeadLetter($"Exceeded max job age ({_options.MaxJobAgeDays} days)");
        }

        // Calculate next attempt time
        var nextAttemptTime = CalculateNextAttemptTime(job.AttemptCount);

        _logger.LogInformation(
            "Job {JobId} will retry at {NextAttempt} (attempt {AttemptCount}/{MaxAttempts})",
            job.JobId, nextAttemptTime, job.AttemptCount + 1, _options.MaxAttempts);

        return RetryDecision.Retry(
            nextAttemptTime,
            $"Retryable error, attempt {job.AttemptCount + 1}/{_options.MaxAttempts}");
    }

    /// <inheritdoc />
    public bool IsRetryableError(int? httpStatusCode = null, Exception? exception = null)
    {
        // Network exceptions are retryable
        if (exception != null)
        {
            // HttpRequestException covers DNS, connect, timeout, TLS errors
            if (exception is HttpRequestException ||
                exception is TaskCanceledException ||
                exception is TimeoutException)
            {
                return true;
            }
        }

        // No HTTP status means network failure (retryable)
        if (httpStatusCode == null)
        {
            return true;
        }

        // Check if explicitly non-retryable
        if (_options.NonRetryableHttpStatusCodes.Contains(httpStatusCode.Value))
        {
            return false;
        }

        // Check if explicitly retryable
        if (_options.RetryableHttpStatusCodes.Contains(httpStatusCode.Value))
        {
            return true;
        }

        // Default: 5xx are retryable, 4xx are not (except those explicitly listed)
        if (httpStatusCode >= 500 && httpStatusCode <= 599)
        {
            return true;
        }

        if (httpStatusCode >= 400 && httpStatusCode <= 499)
        {
            return false;
        }

        // Default to retryable for anything else
        return true;
    }

    /// <inheritdoc />
    public DateTime CalculateNextAttemptTime(int attemptCount)
    {
        var now = _clock.UtcNow;
        double delaySeconds;

        // Use explicit retry schedule if provided
        if (_options.RetryScheduleSeconds != null && _options.RetryScheduleSeconds.Length > 0)
        {
            // Use the schedule value for this attempt, or the last value if we've exceeded the schedule
            var scheduleIndex = Math.Min(attemptCount, _options.RetryScheduleSeconds.Length - 1);
            delaySeconds = _options.RetryScheduleSeconds[scheduleIndex];
        }
        else
        {
            // Use exponential backoff formula: InitialDelay * (Multiplier ^ attemptCount)
            delaySeconds = _options.InitialDelaySeconds * Math.Pow(_options.BackoffMultiplier, attemptCount);

            // Cap at max delay
            delaySeconds = Math.Min(delaySeconds, _options.MaxDelaySeconds);
        }

        // Apply jitter: ±JitterFactor% of the delay
        if (_options.JitterFactor > 0)
        {
            var jitterRange = delaySeconds * _options.JitterFactor;
            var jitter = (_random.NextDouble() * 2 - 1) * jitterRange; // Random value between -jitterRange and +jitterRange
            delaySeconds += jitter;

            // Ensure delay is still positive
            delaySeconds = Math.Max(delaySeconds, 1);
        }

        return now.AddSeconds(delaySeconds);
    }
}

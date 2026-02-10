using FolderToApi.Service.Tests.Helpers;

namespace FolderToApi.Service.Tests.Services;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(500, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    [InlineData(429, true)]
    [InlineData(408, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    [InlineData(403, false)]
    public void IsRetryableError_CorrectlyClassifiesHttpStatusCodes(int statusCode, bool expectedRetryable)
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions());
        var clock = new TestClock();
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act
        var result = policy.IsRetryableError(statusCode, null);

        // Assert
        result.Should().Be(expectedRetryable);
    }

    [Fact]
    public void IsRetryableError_ReturnsTrueForHttpRequestException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions());
        var clock = new TestClock();
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var exception = new HttpRequestException("Network error");

        // Act
        var result = policy.IsRetryableError(null, exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRetryableError_ReturnsTrueForTaskCanceledException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions());
        var clock = new TestClock();
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var exception = new TaskCanceledException("Timeout");

        // Act
        var result = policy.IsRetryableError(null, exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRetry_ReturnsDeadLetter_WhenMaxAttemptsExceeded()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            MaxAttempts = 10
        });
        var clock = new TestClock();
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var job = CreateTestJob();
        job.AttemptCount = 10;

        // Act
        var decision = policy.EvaluateRetry(job, 500, null);

        // Assert
        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().ContainEquivalentOf("max attempts");
    }

    [Fact]
    public void EvaluateRetry_ReturnsDeadLetter_WhenMaxJobAgeExceeded()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            MaxJobAgeDays = 7
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var job = CreateTestJob();
        job.DiscoveredAtUtc = clock.UtcNow.AddDays(-8); // 8 days old

        // Act
        var decision = policy.EvaluateRetry(job, 500, null);

        // Assert
        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().ContainEquivalentOf("max job age");
    }

    [Fact]
    public void EvaluateRetry_ReturnsDeadLetter_WhenNonRetryableError()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions());
        var clock = new TestClock();
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var job = CreateTestJob();

        // Act
        var decision = policy.EvaluateRetry(job, 404, null);

        // Assert
        decision.ShouldRetry.Should().BeFalse();
        decision.Reason.Should().ContainEquivalentOf("non-retryable");
    }

    [Fact]
    public void EvaluateRetry_ReturnsRetry_WhenWithinLimits()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            MaxAttempts = 10,
            InitialDelaySeconds = 10
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        var job = CreateTestJob();
        job.AttemptCount = 3;

        // Act
        var decision = policy.EvaluateRetry(job, 503, null);

        // Assert
        decision.ShouldRetry.Should().BeTrue();
        decision.NextAttemptAtUtc.Should().NotBeNull();
        decision.NextAttemptAtUtc.Value.Should().BeAfter(clock.UtcNow);
    }

    [Fact]
    public void CalculateNextAttemptTime_UsesExponentialBackoff()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            InitialDelaySeconds = 10,
            BackoffMultiplier = 2.0,
            MaxDelaySeconds = 3600,
            JitterFactor = 0 // No jitter for deterministic test
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act & Assert - Test exponential progression
        var next0 = policy.CalculateNextAttemptTime(0);
        var delay0 = (next0 - clock.UtcNow).TotalSeconds;
        delay0.Should().BeApproximately(10, 1); // 10 * 2^0 = 10

        var next1 = policy.CalculateNextAttemptTime(1);
        var delay1 = (next1 - clock.UtcNow).TotalSeconds;
        delay1.Should().BeApproximately(20, 1); // 10 * 2^1 = 20

        var next2 = policy.CalculateNextAttemptTime(2);
        var delay2 = (next2 - clock.UtcNow).TotalSeconds;
        delay2.Should().BeApproximately(40, 1); // 10 * 2^2 = 40

        var next3 = policy.CalculateNextAttemptTime(3);
        var delay3 = (next3 - clock.UtcNow).TotalSeconds;
        delay3.Should().BeApproximately(80, 1); // 10 * 2^3 = 80
    }

    [Fact]
    public void CalculateNextAttemptTime_RespectsMaxDelay()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            InitialDelaySeconds = 10,
            BackoffMultiplier = 2.0,
            MaxDelaySeconds = 100,
            JitterFactor = 0 // No jitter for deterministic test
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act
        var nextAttempt = policy.CalculateNextAttemptTime(20); // Would be huge without max delay
        var delay = (nextAttempt - clock.UtcNow).TotalSeconds;

        // Assert
        delay.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public void CalculateNextAttemptTime_UsesRetrySchedule_WhenProvided()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            RetryScheduleSeconds = new int[] { 10, 30, 120, 300 },
            JitterFactor = 0 // No jitter for deterministic test
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act & Assert
        var delay0 = (policy.CalculateNextAttemptTime(0) - clock.UtcNow).TotalSeconds;
        delay0.Should().BeApproximately(10, 1);

        var delay1 = (policy.CalculateNextAttemptTime(1) - clock.UtcNow).TotalSeconds;
        delay1.Should().BeApproximately(30, 1);

        var delay2 = (policy.CalculateNextAttemptTime(2) - clock.UtcNow).TotalSeconds;
        delay2.Should().BeApproximately(120, 1);

        var delay3 = (policy.CalculateNextAttemptTime(3) - clock.UtcNow).TotalSeconds;
        delay3.Should().BeApproximately(300, 1);

        // Beyond schedule length, use last value
        var delay10 = (policy.CalculateNextAttemptTime(10) - clock.UtcNow).TotalSeconds;
        delay10.Should().BeApproximately(300, 1);
    }

    [Fact]
    public void CalculateNextAttemptTime_AppliesJitter()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            InitialDelaySeconds = 100,
            BackoffMultiplier = 1.0,
            JitterFactor = 0.2 // ±20% jitter
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act - Run 50 times to get statistical variance
        var delays = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var nextAttempt = policy.CalculateNextAttemptTime(0);
            var delay = (nextAttempt - clock.UtcNow).TotalSeconds;
            delays.Add(delay);
        }

        // Assert
        // With 20% jitter, delays should be in range [80, 120]
        delays.Should().AllSatisfy(d =>
        {
            d.Should().BeGreaterOrEqualTo(80);
            d.Should().BeLessOrEqualTo(120);
        });

        // Results should show variance (not all the same)
        var uniqueDelays = delays.Distinct().Count();
        uniqueDelays.Should().BeGreaterThan(10, "jitter should produce varied results");
    }

    [Fact]
    public void CalculateNextAttemptTime_JitterNeverResultsInNegativeDelay()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RetryPolicy>>();
        var options = Options.Create(new RetryPolicyOptions
        {
            InitialDelaySeconds = 1,
            BackoffMultiplier = 1.0,
            JitterFactor = 0.49 // Extreme jitter (max allowed is 0.5)
        });
        var clock = new TestClock(DateTime.UtcNow);
        var policy = new RetryPolicy(options, clock, mockLogger.Object);

        // Act - Run many times
        for (int i = 0; i < 100; i++)
        {
            var nextAttempt = policy.CalculateNextAttemptTime(0);
            var delay = (nextAttempt - clock.UtcNow).TotalSeconds;

            // Assert
            delay.Should().BeGreaterThan(0, "delay should never be negative or zero");
        }
    }

    private Job CreateTestJob()
    {
        return new Job
        {
            JobId = Guid.NewGuid().ToString(),
            SourcePath = "/test/file.pdf",
            ProcessingPath = "/processing/file.pdf",
            FileSizeBytes = 1024000,
            DiscoveredAtUtc = DateTime.UtcNow,
            Status = JobStatus.RetryScheduled,
            AttemptCount = 0
        };
    }
}

# US-012: Retry Policy and Exponential Backoff Strategy

**As a** Developer,
**I want to** implement a RetryPolicy that calculates exponential backoff with jitter and capped retries,
**So that** failed deliveries are retried intelligently without overwhelming the API or creating excessive load.

## Acceptance Criteria
- [ ] **Scenario 1**: Calculate Exponential Backoff Delay
    - **Given** a job has failed and needs to be retried
    - **When** the RetryPolicy calculates the next attempt time
    - **Then** the delay increases exponentially: 10s, 30s, 2m, 5m, 15m, 30m, 60m (configurable schedule), capped at a maximum delay

- [ ] **Scenario 2**: Add Jitter to Prevent Thundering Herd
    - **Given** multiple jobs may fail simultaneously
    - **When** backoff delays are calculated
    - **Then** random jitter (±20% of delay) is added to prevent all retries from happening at the exact same time

- [ ] **Scenario 3**: Respect Maximum Attempts Limit
    - **Given** a job has reached MaxAttempts (e.g., 10)
    - **When** the retry policy is evaluated
    - **Then** the job is marked for dead-letter (no more retries) and moved to Failed status

- [ ] **Scenario 4**: Enforce Maximum Job Age
    - **Given** a job has been in the system for MaxJobAgeDays (e.g., 7 days)
    - **When** the retry policy is evaluated
    - **Then** the job is marked for dead-letter regardless of attempt count

- [ ] **Scenario 5**: Determine if Error is Retryable
    - **Given** a delivery attempt failed with a specific HTTP status or exception
    - **When** the retry policy evaluates the error
    - **Then** the policy returns IsRetryable=true for 5xx/408/429/network errors and IsRetryable=false for most 4xx errors

- [ ] **Scenario 6**: Return Next Attempt Timestamp
    - **Given** a retry is scheduled
    - **When** the RetryPolicy calculates the next attempt
    - **Then** the next_attempt_at_utc timestamp is returned for storage in the job record

- [ ] **Scenario 7**: Support Configurable Backoff Schedule
    - **Given** different environments may need different retry strategies
    - **When** the RetryPolicy is configured
    - **Then** the backoff schedule is loaded from configuration (array of delays in seconds)

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Exponential backoff with jitter formula: `delay = baseDelay * 2^attempt ± jitter`
- Example schedule (configurable): [10s, 30s, 120s, 300s, 900s, 1800s, 3600s]
- Jitter: random value between 0.8x and 1.2x of calculated delay (configurable)
- Configuration:
  - RetryScheduleSeconds: int[] (array of delays)
  - MaxAttempts: int (default 10)
  - MaxJobAgeDays: int (default 7)
  - JitterPercentage: double (default 20%)
- Retryable errors:
  - Network failures (DNS, connect, timeout)
  - HTTP 408, 429, 5xx
- Non-retryable errors:
  - HTTP 400, 401, 403, 404, 415, etc.
- Return: RetryDecision { ShouldRetry: bool, NextAttemptAtUtc: DateTime?, Reason: string }
- Inject IClock for testable time

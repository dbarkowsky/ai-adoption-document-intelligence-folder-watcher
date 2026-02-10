# US-013: Send Loop Hosted Service with Concurrent Delivery

**As a** Developer,
**I want to** implement a SendLoopHostedService that continuously queries and sends due jobs with bounded concurrency,
**So that** files are delivered to the API as soon as they're ready while controlling resource usage and API load.

## Acceptance Criteria
- [ ] **Scenario 1**: Query and Dequeue Due Jobs
    - **Given** the send loop is running
    - **When** it queries for due jobs
    - **Then** JobRepository.GetDueJobs() returns all jobs with status in (Claimed, RetryScheduled) and next_attempt_at_utc <= now, ordered by next_attempt_at_utc

- [ ] **Scenario 2**: Send Files with Bounded Concurrency
    - **Given** multiple jobs are due for sending
    - **When** the send loop processes them
    - **Then** a maximum of N concurrent uploads are active at any time (default 5, configurable via SemaphoreSlim)

- [ ] **Scenario 3**: Update Job Status Before Sending
    - **Given** a job is dequeued for sending
    - **When** the send begins
    - **Then** the job status is set to Sending, attempt_count is incremented, and this state is persisted in SQLite

- [ ] **Scenario 4**: Handle Successful Delivery
    - **Given** the API returns HTTP 2xx
    - **When** the send completes
    - **Then** the job is marked Sent, sent_at_utc is recorded, the file is moved to `<Root>\sent`, and the job is removed from the active queue

- [ ] **Scenario 5**: Handle Retryable Failure
    - **Given** the API returns HTTP 5xx or a network error occurs
    - **When** the send fails
    - **Then** the retry policy calculates next_attempt_at_utc, the job is marked RetryScheduled with error details, and the file remains in processing

- [ ] **Scenario 6**: Handle Permanent Failure (Dead Letter)
    - **Given** a job fails with a non-retryable error or exceeds MaxAttempts
    - **When** the failure is processed
    - **Then** the job is marked Failed, the file is moved to `<Root>\failed`, and error details are preserved for forensics

- [ ] **Scenario 7**: Handle Job File Missing from Processing
    - **Given** a job is ready to send but the file is missing from processing
    - **When** the send is attempted
    - **Then** the error is logged, the job is marked Failed with reason "File missing", and an alert is raised

- [ ] **Scenario 8**: Stop Sending on Service Shutdown
    - **Given** the service receives a shutdown signal
    - **When** the CancellationToken is triggered
    - **Then** the send loop stops accepting new jobs, waits for in-flight sends to complete (with timeout), and exits gracefully

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Implement as BackgroundService or IHostedService
- Use SemaphoreSlim for bounded concurrency (default 5 concurrent uploads)
- Send loop pattern:
  1. Query due jobs (batch query)
  2. For each job (with semaphore): send → update state → move file
  3. Sleep briefly if no jobs, then query again
- State transitions:
  - Before send: status → Sending, increment attempt_count
  - Success: status → Sent, move file to sent
  - Retry: status → RetryScheduled, calculate next_attempt_at_utc
  - Permanent failure: status → Failed, move file to failed
- All state updates must be transactional in SQLite
- Log all send attempts with structured logging: job_id, filename, attempt_count, status, http_status, latency_ms, error
- Inject: IOptions<Config>, JobRepository, ApiClient, RetryPolicy, ILogger
- Register as: `services.AddHostedService<SendLoopHostedService>()`
- Graceful shutdown: use CancellationToken and wait for semaphore to drain

# US-014: Dead Letter Handling and Forensics

**As an** Operations Team member,
**I want to** automatically move permanently failed files to a dead-letter folder with detailed error logging,
**So that** I can review and troubleshoot failed deliveries without losing files or error context.

## Acceptance Criteria
- [ ] **Scenario 1**: Move File to Failed Folder on Permanent Failure
    - **Given** a job has exceeded MaxAttempts or encountered a non-retryable error
    - **When** the job is marked as Failed
    - **Then** the file is moved from `<Root>\processing\<jobId>\` to `<Root>\failed\<jobId>-<filename>` and the directory structure is preserved for forensics

- [ ] **Scenario 2**: Record Error Details in SQLite
    - **Given** a job is being marked as Failed
    - **When** the dead-letter operation executes
    - **Then** the job record preserves: last_error, last_http_status, attempt_count, discovered_at_utc, and all other metadata for forensics

- [ ] **Scenario 3**: Log Operator-Facing Error Event
    - **Given** a job moves to dead-letter
    - **When** the status is updated to Failed
    - **Then** an ERROR-level log event is written with: job_id, filename, reason, last_http_status, attempt_count, and total age

- [ ] **Scenario 4**: Handle Dead Letter Due to MaxAttempts Exceeded
    - **Given** a job has reached MaxAttempts (e.g., 10)
    - **When** the retry policy evaluates the job
    - **Then** the job is moved to dead-letter with reason "Max attempts exceeded"

- [ ] **Scenario 5**: Handle Dead Letter Due to MaxJobAge Exceeded
    - **Given** a job has been in the system for more than MaxJobAgeDays (e.g., 7 days)
    - **When** the retry policy evaluates the job
    - **Then** the job is moved to dead-letter with reason "Max job age exceeded"

- [ ] **Scenario 6**: Handle Dead Letter Due to Permanent HTTP Error
    - **Given** the API returns HTTP 400, 401, 403, 404, or 415
    - **When** the response is processed
    - **Then** the job is immediately moved to dead-letter with reason "Permanent HTTP error: {status}"

- [ ] **Scenario 7**: Preserve File Naming for Easy Identification
    - **Given** a file is moved to the failed folder
    - **When** the move completes
    - **Then** the filename includes the jobId prefix (e.g., `<jobId>-original-filename.pdf`) for easy correlation with database records

- [ ] **Scenario 8**: Query Failed Jobs for Manual Review
    - **Given** an operator needs to review failed jobs
    - **When** they query the database
    - **Then** all jobs with status=Failed can be retrieved with full error details and file location

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Failed folder structure: `<Root>\failed\<jobId>-<filename>`
- Preserve all job metadata in SQLite even after moving to failed
- Dead-letter triggers:
  - attempt_count >= MaxAttempts
  - job age > MaxJobAgeDays
  - Non-retryable HTTP status (4xx except allowlisted)
- Error logging must include:
  - job_id, filename, source_path, processing_path
  - status, attempt_count, last_error, last_http_status
  - discovered_at_utc, claimed_at_utc
  - total job age in hours/days
- Retention: failed files should be retained longer than sent files (configurable)
- Future enhancement: retry mechanism to move files from failed back to inbox for reprocessing
- Implement FailedJobQuery in JobRepository for operator review

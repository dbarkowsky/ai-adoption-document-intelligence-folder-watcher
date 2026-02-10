# US-005: Job Repository CRUD Operations with Transactional State Management

**As a** Developer,
**I want to** implement a JobRepository with all CRUD operations and transactional state management,
**So that** I can reliably create, update, query, and transition jobs through their lifecycle with ACID guarantees.

## Acceptance Criteria
- [ ] **Scenario 1**: Create New Job with Discovery Metadata
    - **Given** a new file is discovered in the inbox
    - **When** the scanner creates a new job
    - **Then** the job is inserted with job_id (GUID), source_path, status=Discovered, discovered_at_utc, file_size_bytes, file_mtime_utc, and attempt_count=0

- [ ] **Scenario 2**: Update Job Status Transactionally
    - **Given** a job needs to transition from one state to another
    - **When** UpdateJobStatus is called within a transaction
    - **Then** the job status, timestamps, and related fields are updated atomically and the transaction commits or rolls back on error

- [ ] **Scenario 3**: Query Due Jobs for Sending
    - **Given** the send loop needs to find jobs ready for delivery
    - **When** GetDueJobs() is called
    - **Then** all jobs with status in (Claimed, RetryScheduled) and next_attempt_at_utc <= now are returned, ordered by next_attempt_at_utc

- [ ] **Scenario 4**: Increment Attempt Count and Schedule Retry
    - **Given** a job failed and needs to be retried
    - **When** RecordRetry() is called with error details and next attempt time
    - **Then** attempt_count is incremented, last_error and last_http_status are recorded, next_attempt_at_utc is set, and status is set to RetryScheduled

- [ ] **Scenario 5**: Mark Job as Sent Successfully
    - **Given** a file was successfully delivered to the API
    - **When** MarkJobSent() is called
    - **Then** the job status is set to Sent, sent_at_utc is recorded, and the final response metadata is stored

- [ ] **Scenario 6**: Mark Job as Failed (Dead Letter)
    - **Given** a job exceeded max attempts or encountered a permanent failure
    - **When** MarkJobFailed() is called
    - **Then** the job status is set to Failed, last_error and last_http_status are preserved for forensics

- [ ] **Scenario 7**: Check for Existing Job by Source Path
    - **Given** the scanner needs to avoid re-claiming already processed files
    - **When** FindJobBySourcePath() is called
    - **Then** the repository returns the existing job if found, or null if not found

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- All state transitions must be transactional (use ADO.NET transactions)
- Use parameterized queries to prevent SQL injection
- Implement proper exception handling and transaction rollback
- Key methods:
  - CreateJob(jobId, sourcePath, metadata)
  - UpdateJobStatus(jobId, status, timestamp)
  - GetDueJobs(currentTimeUtc)
  - RecordRetry(jobId, error, httpStatus, nextAttemptTime)
  - MarkJobSent(jobId, sentTimeUtc)
  - MarkJobFailed(jobId, error, httpStatus)
  - FindJobBySourcePath(sourcePath)
  - FindJobByJobId(jobId)
- Repository should be registered as singleton in DI
- Consider connection pooling and proper disposal of connections/commands

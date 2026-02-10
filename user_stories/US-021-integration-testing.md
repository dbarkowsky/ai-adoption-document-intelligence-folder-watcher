# US-021: Integration Testing with Real Dependencies

**As a** Developer,
**I want to** create integration tests that verify end-to-end scenarios with real filesystem, SQLite, and HTTP,
**So that** I can validate the service works correctly with actual dependencies before deployment.

## Acceptance Criteria
- [ ] **Scenario 1**: Test File Discovery and Claiming on Local Disk
    - **Given** a test folder is set up with sample files
    - **When** the scan loop runs
    - **Then** files are discovered, completeness-checked, claimed, and moved to processing with job records created in SQLite

- [ ] **Scenario 2**: Test File Discovery and Claiming on SMB Share
    - **Given** a test SMB share is configured with sample files
    - **When** the scan loop runs
    - **Then** files are discovered and claimed from the UNC path, and cross-volume copy-verify-delete logic is tested if applicable

- [ ] **Scenario 3**: Test Locked File Handling
    - **Given** a file is being written by another process
    - **When** the scanner attempts to check completeness
    - **Then** the file is detected as locked, skipped for this scan, and successfully claimed on a subsequent scan after the lock is released

- [ ] **Scenario 4**: Test Successful HTTP Delivery
    - **Given** a mock HTTP server is running and accepting files
    - **When** a job is sent
    - **Then** the file is uploaded successfully, the API returns 200, the job is marked Sent, and the file is moved to the sent folder

- [ ] **Scenario 5**: Test Retry on 5xx Errors
    - **Given** a mock HTTP server returns 500 for the first 2 attempts
    - **When** the send loop retries
    - **Then** the job is retried with backoff delays, attempt_count is incremented, and the job eventually succeeds on the 3rd attempt

- [ ] **Scenario 6**: Test Dead-Letter on Permanent 4xx Error
    - **Given** a mock HTTP server returns 403 Forbidden
    - **When** the send is attempted
    - **Then** the job is immediately marked Failed, moved to the failed folder, and no retries are scheduled

- [ ] **Scenario 7**: Test Service Restart and Resume
    - **Given** the service is running with jobs in progress
    - **When** the service is stopped and restarted
    - **Then** jobs in Claimed or RetryScheduled status are resumed, and jobs in Sending status are reset to RetryScheduled

- [ ] **Scenario 8**: Test Throughput with 100+ Files
    - **Given** the inbox contains 100+ sample files
    - **When** the service processes the batch
    - **Then** all files are claimed, sent successfully, and moved to the sent folder within a reasonable time (verify performance)

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Integration test project: separate from unit tests
- Use real SQLite database (create temp database for each test run)
- Use real filesystem (create temp directories for each test)
- Mock HTTP server: use WireMock.Net or TestServer (ASP.NET Core)
- Test scenarios:
  - Happy path: discover → claim → send → sent
  - Locked file: skip → retry → success
  - Retryable error: 5xx → retry with backoff → success
  - Permanent error: 4xx → dead-letter
  - Service restart: resume jobs correctly
  - Cross-volume: test UNC paths if available
- Use Docker or local SMB share for UNC path tests (optional)
- Test fixtures:
  - Create sample files (PDF, DOCX, TIF)
  - Set up test folders
  - Start/stop mock HTTP server
  - Clean up after each test
- Performance test: verify 100+ files processed in < 5 minutes with realistic settings

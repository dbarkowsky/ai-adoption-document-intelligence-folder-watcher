# US-022: Resilience Testing for Service Restarts and Network Outages

**As a** QA Engineer,
**I want to** verify the service recovers correctly from restarts, reboots, and network outages,
**So that** guaranteed delivery is maintained even under adverse conditions.

## Acceptance Criteria
- [ ] **Scenario 1**: Test Service Restart Mid-Send
    - **Given** a file upload is in progress
    - **When** the service is forcefully stopped
    - **Then** after restart, the job is reset to RetryScheduled and successfully delivered on the next attempt

- [ ] **Scenario 2**: Test Server Reboot
    - **Given** the Windows Server is rebooted with jobs in processing
    - **When** the server restarts and the service auto-starts
    - **Then** all jobs resume correctly, and no files or job records are lost or corrupted

- [ ] **Scenario 3**: Test SMB Share Outage During Scan
    - **Given** the inbox is on an SMB share
    - **When** the SMB share becomes unavailable during a scan
    - **Then** the scan fails gracefully, errors are logged, and the next scan retries successfully when the share is available

- [ ] **Scenario 4**: Test API Downtime and Recovery
    - **Given** the remote API is down for 10 minutes
    - **When** jobs attempt to send during the outage
    - **Then** jobs are retried with exponential backoff, and all jobs are successfully delivered once the API recovers

- [ ] **Scenario 5**: Test Network Timeout Handling
    - **Given** the API is slow and requests timeout
    - **When** send attempts exceed the timeout
    - **Then** the timeout is caught, logged as a retryable error, and the job is retried with backoff

- [ ] **Scenario 6**: Test SQLite Database Recovery After Crash
    - **Given** the service crashes during a database transaction
    - **When** the service restarts
    - **Then** SQLite WAL recovery completes, no corruption occurs, and jobs resume correctly

- [ ] **Scenario 7**: Test Graceful Shutdown with In-Flight Operations
    - **Given** files are being claimed and sent
    - **When** the service receives a shutdown signal
    - **Then** in-flight operations complete (or timeout gracefully), state is checkpointed, and the service exits cleanly

- [ ] **Scenario 8**: Test Multiple Restarts in Short Succession
    - **Given** the service is restarted 5 times in 1 minute
    - **When** each restart occurs
    - **Then** the service initializes correctly each time, no duplicate jobs are created, and processing resumes normally

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Resilience test scenarios:
  - Restart during scan, claim, send
  - Server reboot (manual or automated test)
  - Network partition (simulate with firewall rules or test harness)
  - API downtime (simulate with mock server or pause)
  - SQLite crash recovery (force-kill process during write)
- Test harness:
  - Script to start/stop service programmatically
  - Network simulator or mock server with controllable availability
  - SMB share simulator or Docker container
- Verify after each scenario:
  - No lost files
  - No duplicate jobs
  - SQLite database integrity (no corruption)
  - All jobs eventually reach Sent or Failed status
  - Logs contain appropriate error and recovery messages
- Use Windows Server VM or Docker for testing
- Automated tests where possible; manual tests for full server reboot
- Document manual test procedures for scenarios that can't be automated

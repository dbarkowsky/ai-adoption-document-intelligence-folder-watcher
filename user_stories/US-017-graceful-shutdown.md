# US-017: Graceful Shutdown and State Consistency

**As a** System Administrator,
**I want to** ensure the service shuts down gracefully without corrupting state or losing files,
**So that** restarts, updates, and server reboots don't cause data loss or SQLite corruption.

## Acceptance Criteria
- [ ] **Scenario 1**: Stop Accepting New Work on Shutdown Signal
    - **Given** the service receives a stop signal (Ctrl+C or service stop command)
    - **When** the CancellationToken is triggered
    - **Then** the scan loop stops discovering new files and the send loop stops dequeuing new jobs

- [ ] **Scenario 2**: Complete In-Flight Operations
    - **Given** files are being claimed or sent when shutdown is requested
    - **When** the shutdown sequence begins
    - **Then** in-flight claim and send operations are allowed to complete (with a timeout, e.g., 30 seconds)

- [ ] **Scenario 3**: Checkpoint State Before Exit
    - **Given** active operations are completing
    - **When** operations finish or timeout is reached
    - **Then** all SQLite transactions are committed or rolled back cleanly, and no database corruption occurs

- [ ] **Scenario 4**: Handle Forced Termination
    - **Given** the service is forcibly killed (kill -9, server crash)
    - **When** the service restarts
    - **Then** jobs in status=Sending are detected and re-queued for retry (e.g., reset to RetryScheduled)

- [ ] **Scenario 5**: Log Shutdown Sequence
    - **Given** the service is shutting down
    - **When** shutdown begins and completes
    - **Then** structured logs record: shutdown initiated, in-flight operations count, shutdown completed, total uptime

- [ ] **Scenario 6**: Resume After Restart
    - **Given** the service was shut down gracefully or forcefully
    - **When** the service restarts
    - **Then** jobs in Claimed or RetryScheduled status are processed normally, and no duplicate jobs are created

- [ ] **Scenario 7**: Prevent SQLite Corruption
    - **Given** SQLite database may be accessed during shutdown
    - **When** the service stops
    - **Then** all open connections are closed, transactions are committed or rolled back, and WAL is checkpointed

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use CancellationToken to signal shutdown to all hosted services
- BackgroundService.StopAsync receives CancellationToken
- Graceful shutdown sequence:
  1. Signal shutdown via CancellationToken
  2. Stop scan loop (no new discoveries)
  3. Stop send loop (no new dequeues)
  4. Wait for in-flight operations (claim, send) to complete
  5. Timeout: 30 seconds (configurable)
  6. Close all SQLite connections
  7. Exit process
- On startup recovery:
  - Query for jobs in status=Sending (orphaned by crash)
  - Reset to status=RetryScheduled with immediate next_attempt_at
- Windows Service graceful shutdown:
  - Service Control Manager sends stop signal
  - Host.StopAsync is called
  - BackgroundService.StopAsync is invoked with CancellationToken
- Prevent corruption:
  - Use WAL mode
  - All transactions must commit or rollback before exit
  - Close connections cleanly
- Log all shutdown events for troubleshooting

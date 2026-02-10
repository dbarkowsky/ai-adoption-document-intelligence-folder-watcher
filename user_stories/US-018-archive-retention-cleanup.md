# US-018: Archive Retention and Cleanup for Sent and Failed Files

**As a** System Administrator,
**I want to** automatically purge old files from the sent and failed folders based on retention policies,
**So that** disk space is managed effectively and old files don't accumulate indefinitely.

## Acceptance Criteria
- [ ] **Scenario 1**: Purge Sent Files Older Than Retention Period
    - **Given** the sent folder contains files older than SentRetentionDays (e.g., 30 days)
    - **When** the cleanup task runs
    - **Then** files in `<Root>\sent` with LastWriteTimeUtc older than the retention period are deleted

- [ ] **Scenario 2**: Retain Failed Files Longer Than Sent Files
    - **Given** failed files require manual review
    - **When** the cleanup task runs
    - **Then** files in `<Root>\failed` are retained for FailedRetentionDays (e.g., 90 days), which is longer than sent files

- [ ] **Scenario 3**: Run Cleanup on a Schedule
    - **Given** cleanup should not block normal operations
    - **When** the service is running
    - **Then** a background cleanup task runs daily (or at configured intervals) to purge old files

- [ ] **Scenario 4**: Log Cleanup Operations
    - **Given** cleanup is running
    - **When** files are purged
    - **Then** logs include: timestamp, folder (sent/failed), files_deleted_count, total_space_freed_mb

- [ ] **Scenario 5**: Handle Cleanup Errors Gracefully
    - **Given** a file cannot be deleted (locked, permissions issue)
    - **When** cleanup attempts to delete it
    - **Then** the error is logged but cleanup continues with remaining files

- [ ] **Scenario 6**: Optionally Purge Completed Job Records
    - **Given** the SQLite database may grow with completed job records
    - **When** job records are older than a configured period
    - **Then** jobs with status=Sent and sent_at_utc older than JobRecordRetentionDays are optionally purged from the database

- [ ] **Scenario 7**: Support Dry-Run Mode for Testing
    - **Given** administrators want to test cleanup without deleting files
    - **When** cleanup runs in dry-run mode
    - **Then** files that would be deleted are logged but not actually deleted

## Priority
- [ ] High (Must Have)
- [x] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Retention policies (configurable):
  - SentRetentionDays: default 30 days
  - FailedRetentionDays: default 90 days (longer for forensics)
  - JobRecordRetentionDays: optional, default 90 days
- Cleanup schedule: daily at configured time (e.g., 2:00 AM) or interval-based
- Implement as a separate BackgroundService or scheduled task
- File deletion criteria: LastWriteTimeUtc or sent_at_utc/failed_at_utc from job record
- Log cleanup summary: files deleted, space freed, duration
- Handle errors: log and continue (don't fail entire cleanup)
- Future enhancement: archive to cold storage instead of delete
- Configuration:
  - EnableCleanup: bool (default true)
  - CleanupScheduleCron: string (e.g., "0 2 * * *")
  - SentRetentionDays: int
  - FailedRetentionDays: int
  - JobRecordRetentionDays: int (optional)
  - DryRunMode: bool (default false)

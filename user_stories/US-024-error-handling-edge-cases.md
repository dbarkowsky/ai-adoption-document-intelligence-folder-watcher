# US-024: Error Handling and Edge Case Coverage

**As a** Developer,
**I want to** handle all edge cases and error conditions gracefully with appropriate logging,
**So that** the service remains stable and issues can be diagnosed quickly.

## Acceptance Criteria
- [x] **Scenario 1**: Handle Missing or Invalid Configuration
    - **Given** required configuration keys are missing or invalid
    - **When** the service starts
    - **Then** the service fails fast with a clear error message indicating which configuration is invalid
    - **Implementation**: Data annotations validation with `ValidateDataAnnotations()` and `ValidateOnStart()` in Program.cs

- [x] **Scenario 2**: Handle Inaccessible Root Path
    - **Given** the configured RootPath is unavailable (network share down, permissions issue)
    - **When** the service attempts to access the path
    - **Then** the error is logged, the scan is skipped for this interval, and retries occur on the next scan
    - **Implementation**: Exception handling in InboxEnumerator.cs and ScanLoopHostedService.cs

- [x] **Scenario 3**: Handle SQLite Database Locked
    - **Given** the SQLite database is locked by another process
    - **When** a transaction is attempted
    - **Then** the operation retries with timeout, or fails gracefully with an error log
    - **Implementation**: `ExecuteWithRetryAsync` helper method in JobRepository.cs with exponential backoff (100ms, 200ms, 400ms, 800ms)

- [x] **Scenario 4**: Handle File Deleted Between Enumeration and Claim
    - **Given** a file is deleted after enumeration but before claiming
    - **When** the claim is attempted
    - **Then** the FileNotFoundException is caught, logged as "File gone", and processing continues
    - **Implementation**: FileNotFoundException handling in FileClaimer.cs returns `ClaimStatus.AlreadyClaimed`

- [x] **Scenario 5**: Handle File Already Exists in Processing
    - **Given** a file with the same name already exists in the processing folder
    - **When** the claim attempts to move the file
    - **Then** a unique filename is generated (e.g., append GUID) or the error is logged and the file is skipped
    - **Implementation**: Filename conflict resolution in FileClaimer.cs appends 8-character GUID suffix

- [x] **Scenario 6**: Handle Oversized Files
    - **Given** a file exceeds MaxFileSizeMB
    - **When** the enumerator filters files
    - **Then** the file is skipped and logged with reason "File too large"
    - **Implementation**: Size filtering in InboxEnumerator.cs with warning-level logging

- [x] **Scenario 7**: Handle Invalid File Extensions
    - **Given** a file with an unexpected extension appears in the inbox
    - **When** the enumerator filters files
    - **Then** the file is skipped and logged with reason "Invalid extension"
    - **Implementation**: Extension filtering in InboxEnumerator.cs with debug-level logging

- [x] **Scenario 8**: Handle API Returns Unexpected Status Code
    - **Given** the API returns an unexpected status code (e.g., 418 I'm a teapot)
    - **When** the response is processed
    - **Then** the error is logged, and the retry policy determines if the job should retry or fail
    - **Implementation**: Status code handling in ApiClient.cs with `IsRetryableStatusCode` method (408, 429, 5xx = retryable; 4xx = permanent)

- [x] **Scenario 9**: Handle Zero-Byte Files
    - **Given** a zero-byte file is discovered in the inbox
    - **When** the file is processed
    - **Then** the file is either skipped (configurable) or processed normally, and the behavior is logged
    - **Implementation**: Configurable via `SkipZeroByteFiles` in FolderWatcherOptions.cs, filtering logic in InboxEnumerator.cs

- [x] **Scenario 10**: Handle Long File Names and Paths
    - **Given** a file has a very long name (> 260 characters total path)
    - **When** the file is processed
    - **Then** the error is caught, logged with details, and the file is moved to failed with reason "Path too long"
    - **Implementation**: Path length validation in FileClaimer.cs, configurable via `MaxPathLength` in FolderWatcherOptions.cs (default: 260)

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- All exceptions should be caught and logged with context (job_id, filename, operation)
- Never let unhandled exceptions crash the service (except in fail-fast scenarios on startup)
- Error categories:
  - Configuration errors: fail fast on startup
  - Transient errors: log and retry (network, file locks, API 5xx)
  - Permanent errors: log and dead-letter (4xx, invalid files)
  - Unexpected errors: log with full stack trace and context
- Edge cases to handle:
  - File disappears during processing
  - File name conflicts
  - Path too long (> 260 chars)
  - Zero-byte files
  - Special characters in filenames
  - Concurrent access to same file
  - SQLite busy/locked
  - Network timeouts
  - Disk full
- Use structured exception handling with try-catch blocks
- Log levels:
  - Debug: expected skips (filtered files)
  - Warning: transient errors
  - Error: permanent failures, dead-letter
  - Critical: unrecoverable errors, service shutdown

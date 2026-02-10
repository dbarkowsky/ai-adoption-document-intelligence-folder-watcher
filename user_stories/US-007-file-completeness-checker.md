# US-007: File Completeness Checker with Exclusive Open

**As a** Developer,
**I want to** implement a CompletenessChecker that verifies files are complete using exclusive open,
**So that** the service only processes files that are not being written to and avoids corrupted or partial uploads.

## Acceptance Criteria
- [ ] **Scenario 1**: Check File with Exclusive Open
    - **Given** a file exists in the inbox
    - **When** CompletenessChecker attempts to open it with FileShare.None
    - **Then** the checker tries to open the file for read with no sharing; if successful, the file is marked complete; if it fails with sharing violation, the file is marked locked

- [ ] **Scenario 2**: Handle Locked Files Gracefully
    - **Given** a file is being written by another process
    - **When** the completeness check runs
    - **Then** the check fails with a sharing violation, the file is skipped for this scan, and the scanner will retry on the next interval

- [ ] **Scenario 3**: Optional Stability Heuristic
    - **Given** the configuration specifies StableAgeSeconds = 10
    - **When** the completeness checker evaluates a file
    - **Then** the file's LastWriteTimeUtc must be older than 10 seconds before the exclusive open is attempted (reduces repeated lock attempts)

- [ ] **Scenario 4**: Close File Immediately After Success
    - **Given** the exclusive open succeeds
    - **When** the file is opened
    - **Then** the file handle is closed immediately and the file is marked as complete and ready for claiming

- [ ] **Scenario 5**: Handle File Disappearance During Check
    - **Given** a file is deleted between enumeration and completeness check
    - **When** the checker attempts to open the file
    - **Then** the FileNotFoundException is caught, logged, and the file is marked as "Gone" (not an error)

- [ ] **Scenario 6**: Return Clear Status Result
    - **Given** the completeness check completes
    - **When** the checker returns the result
    - **Then** the result clearly indicates: Complete, Locked, TooNew, Gone, or Error with details

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use `FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)` for exclusive open
- Close the stream immediately after successful open (dispose in try-finally)
- StableAgeSeconds default: 5-10 seconds (configurable)
- Do NOT spin-wait or retry immediately; rely on periodic scan schedule
- Return enum: FileCompletenessStatus { Complete, Locked, TooNew, Gone, Error }
- Handle exceptions:
  - IOException with sharing violation → Locked
  - FileNotFoundException → Gone
  - UnauthorizedAccessException → Error (log and skip)
- Log locked files at Debug level; log errors at Warning level

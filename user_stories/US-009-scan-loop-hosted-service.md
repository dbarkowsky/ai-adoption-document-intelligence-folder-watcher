# US-009: Scan Loop Hosted Service with Periodic Scanning

**As a** Developer,
**I want to** implement a ScanLoopHostedService that periodically scans the inbox and claims eligible files,
**So that** new files are discovered and processed on a regular schedule without missing any files.

## Acceptance Criteria
- [ ] **Scenario 1**: Start Periodic Scanning on Service Start
    - **Given** the service is started
    - **When** ScanLoopHostedService starts executing
    - **Then** the scan loop begins running every ScanIntervalSeconds (default 30, range 10-120)

- [ ] **Scenario 2**: Enumerate and Filter Files Each Scan
    - **Given** it's time for a scheduled scan
    - **When** the scan executes
    - **Then** the InboxEnumerator discovers all files, and filters are applied (extension, size, ignore patterns)

- [ ] **Scenario 3**: Check File Completeness Before Claiming
    - **Given** eligible files are found in the scan
    - **When** the scanner processes each file
    - **Then** the CompletenessChecker performs exclusive open and stability age checks; locked or too-new files are skipped

- [ ] **Scenario 4**: Claim Files and Create Jobs
    - **Given** a file passes the completeness check
    - **When** the scanner claims the file
    - **Then** the Claimer moves it to processing, and a job is created in SQLite with status=Claimed

- [ ] **Scenario 5**: Ensure Deterministic Scanning
    - **Given** repeated scans occur
    - **When** files are scanned multiple times
    - **Then** no duplicate jobs are created (check SQLite for existing jobs by source_path before claiming)

- [ ] **Scenario 6**: Handle Scan Exceptions Without Crashing
    - **Given** an unexpected error occurs during scanning
    - **When** the exception is thrown
    - **Then** the exception is caught, logged with details, and the next scan is scheduled normally (service continues running)

- [ ] **Scenario 7**: Stop Scanning on Service Shutdown
    - **Given** the service receives a shutdown signal
    - **When** the CancellationToken is triggered
    - **Then** the scan loop stops gracefully, completes any in-flight claim operation, and exits cleanly

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Implement as BackgroundService or IHostedService
- Use PeriodicTimer or Task.Delay with CancellationToken for scheduling
- ScanIntervalSeconds: configurable, default 30, range 10-120
- Scan sequence per iteration:
  1. Enumerate inbox files
  2. Apply eligibility filters
  3. For each file: check completeness → check if already claimed → claim if ready
- Determinism: query JobRepository.FindJobBySourcePath() before claiming
- All exceptions must be caught and logged; do not let unhandled exceptions crash the host
- Register as: `services.AddHostedService<ScanLoopHostedService>()`
- Inject: IOptions<Config>, InboxEnumerator, CompletenessChecker, Claimer, JobRepository, ILogger

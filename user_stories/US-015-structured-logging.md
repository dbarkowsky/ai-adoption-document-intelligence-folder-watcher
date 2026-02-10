# US-015: Structured Logging with JSON Format and Log Rotation

**As a** DevOps Engineer,
**I want to** implement structured logging with JSON format and rolling log files,
**So that** I can easily parse logs, troubleshoot issues, and integrate with monitoring tools.

## Acceptance Criteria
- [ ] **Scenario 1**: Configure Structured Logging with JSON Output
    - **Given** the service needs to emit structured logs
    - **When** logging is configured
    - **Then** logs are written in JSON format with fields: timestamp, level, message, job_id, filename, status, attempt_count, http_status, latency_ms, error, source_context

- [ ] **Scenario 2**: Implement Rolling Log Files
    - **Given** log files can grow large over time
    - **When** the logging provider is configured
    - **Then** logs are written to rolling files (e.g., daily or size-based rotation) in a configured directory

- [ ] **Scenario 3**: Log to Windows Event Log
    - **Given** Windows administrators expect Event Log integration
    - **When** the Windows Service logging is configured
    - **Then** critical events (service start/stop, failures) are written to the Windows Event Log with appropriate event IDs

- [ ] **Scenario 4**: Log File Discovery and Claiming
    - **Given** files are being scanned and claimed
    - **When** a file is discovered, completeness-checked, or claimed
    - **Then** logs include: timestamp, level=Information, job_id, filename, source_path, status=Discovered/Claimed, file_size_bytes

- [ ] **Scenario 5**: Log HTTP Requests and Responses
    - **Given** files are being uploaded to the API
    - **When** an HTTP request is sent
    - **Then** logs include: timestamp, level=Information/Warning/Error, job_id, filename, http_method, url, http_status, response_time_ms, attempt_count, error (if any)

- [ ] **Scenario 6**: Log Retry and Dead-Letter Events
    - **Given** a job fails and is retried or moved to dead-letter
    - **When** the retry policy or dead-letter logic executes
    - **Then** logs include: timestamp, level=Warning/Error, job_id, filename, status=RetryScheduled/Failed, attempt_count, next_attempt_at, last_error, last_http_status

- [ ] **Scenario 7**: Log Scan and Send Loop Health
    - **Given** the scan and send loops are running
    - **When** each iteration completes
    - **Then** logs include: timestamp, level=Information, loop_name, files_discovered, jobs_claimed, jobs_sent, jobs_failed, duration_ms

- [ ] **Scenario 8**: Sensitive Data Redaction
    - **Given** API keys and other secrets must not appear in logs
    - **When** logs are written
    - **Then** API keys, authorization headers, and other sensitive fields are redacted or omitted

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use Serilog or similar for structured logging
- JSON formatter for log files
- Rolling file configuration:
  - Path: configurable (default `C:\Logs\FolderToApi\log-.json`)
  - Rolling: daily or size-based (e.g., 100MB)
  - Retention: configurable (e.g., 30 days)
- Windows Event Log integration via `Microsoft.Extensions.Logging.EventLog`
- Log levels:
  - Debug: eligibility filters, completeness checks
  - Information: scan/send iterations, successful operations
  - Warning: retries, transient errors
  - Error: permanent failures, dead-letter events
  - Critical: service startup/shutdown, unrecoverable errors
- Redact sensitive data: API keys, authorization headers
- Include correlation IDs (job_id) in all log entries for tracing

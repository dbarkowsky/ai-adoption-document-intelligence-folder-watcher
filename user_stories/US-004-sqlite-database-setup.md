# US-004: SQLite Database Setup with Schema Management

**As a** Developer,
**I want to** create and manage a SQLite database for durable job state tracking,
**So that** the service can maintain reliable state across restarts and track file processing lifecycle.

## Acceptance Criteria
- [ ] **Scenario 1**: Create SQLite Database and Jobs Table
    - **Given** the service is starting for the first time
    - **When** the database initialization runs
    - **Then** a SQLite database is created with the jobs table containing all required columns: job_id (PK), source_path, processing_path, status, discovered_at_utc, claimed_at_utc, sent_at_utc, attempt_count, next_attempt_at_utc, last_error, last_http_status, file_size_bytes, file_mtime_utc

- [ ] **Scenario 2**: Enable WAL Mode for Concurrency
    - **Given** multiple threads will access the database (scan loop + send loop)
    - **When** the database connection is opened
    - **Then** WAL (Write-Ahead Logging) mode is enabled to improve concurrency characteristics

- [ ] **Scenario 3**: Create Indexes for Query Performance
    - **Given** the service frequently queries jobs by status and next_attempt_at
    - **When** the database schema is initialized
    - **Then** indexes are created on (status, next_attempt_at_utc) and (processing_path) for efficient queries

- [ ] **Scenario 4**: Implement Schema Migration Support
    - **Given** the database schema may evolve over time
    - **When** the service starts
    - **Then** a schema version table exists and the service can detect and apply migrations as needed

- [ ] **Scenario 5**: Verify Database Location on Local Disk
    - **Given** SQLite database must be on local disk for reliability
    - **When** the service initializes the database
    - **Then** the database file is created on a local volume (not SMB) and the path is validated

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use `Microsoft.Data.Sqlite` package (lightweight ADO.NET provider)
- Enable WAL mode with: `PRAGMA journal_mode=WAL;`
- Store database on local disk (not SMB) for reliability and performance
- All state transitions must occur inside transactions
- Schema version table: `schema_version` with columns: version (int), applied_at_utc (text)
- Jobs table status values: Discovered, Claimed, Sending, RetryScheduled, Sent, Failed
- Implement JobRepository as a singleton that owns SQLite connection creation policy
- Each operation should use its own connection/transaction as appropriate

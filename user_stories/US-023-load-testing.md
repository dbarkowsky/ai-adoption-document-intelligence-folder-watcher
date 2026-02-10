# US-023: Load Testing for Sustained Throughput

**As a** QA Engineer,
**I want to** verify the service can handle sustained load of hundreds of files per hour,
**So that** the service meets performance requirements and resource usage remains within acceptable bounds.

## Acceptance Criteria
- [ ] **Scenario 1**: Test 500 Files Per Hour for 4 Hours
    - **Given** the inbox receives 500 files per hour (typical file size 2-5 MB)
    - **When** the service processes the files over 4 hours
    - **Then** all files are successfully delivered, CPU usage stays below 50%, and memory usage remains stable (no leaks)

- [ ] **Scenario 2**: Measure Scan Loop Performance
    - **Given** the inbox contains 1000 files
    - **When** the scan loop enumerates and filters the files
    - **Then** each scan completes in under 30 seconds (configurable based on ScanIntervalSeconds)

- [ ] **Scenario 3**: Measure Send Loop Throughput
    - **Given** 100 jobs are queued for sending
    - **When** the send loop processes them with bounded concurrency (5 concurrent uploads)
    - **Then** all jobs are sent within 10 minutes, and API latency percentiles (p50, p95, p99) are within expected ranges

- [ ] **Scenario 4**: Test Backlog Stability Under Load
    - **Given** files arrive faster than they can be sent (API is rate-limited)
    - **When** the backlog grows to 500+ pending jobs
    - **Then** the service continues to operate correctly, backlog is processed as API capacity allows, and no crashes or resource exhaustion occur

- [ ] **Scenario 5**: Verify Memory Usage Over Time
    - **Given** the service runs continuously for 24 hours under load
    - **When** memory usage is monitored
    - **Then** memory usage remains stable (no significant growth indicating leaks)

- [ ] **Scenario 6**: Verify SQLite Performance
    - **Given** the database contains 10,000+ job records
    - **When** queries are executed (GetDueJobs, FindJobBySourcePath)
    - **Then** queries complete in under 100ms with proper indexes

- [ ] **Scenario 7**: Test Concurrent File Processing
    - **Given** the scan loop and send loop run concurrently
    - **When** both loops are active
    - **Then** no database locking or contention issues occur, and both loops make progress

- [ ] **Scenario 8**: Generate Performance Report
    - **Given** load testing is complete
    - **When** results are compiled
    - **Then** a report is generated with: total files processed, average latency, p95/p99 latency, error rate, CPU/memory usage, and any bottlenecks identified

## Priority
- [ ] High (Must Have)
- [x] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Load test parameters:
  - File count: 500 files/hour for 4 hours = 2000 files
  - File size: 2-5 MB (realistic PDF/DOCX)
  - Concurrent uploads: 5 (configurable)
  - Test duration: 4-24 hours
- Performance targets:
  - Scan duration: < 30 seconds per scan
  - Send throughput: > 50 files/minute (with 5 concurrent uploads)
  - API latency: p95 < 5 seconds, p99 < 10 seconds
  - CPU usage: < 50% average
  - Memory usage: stable (< 500 MB growth over 24 hours)
- Tools:
  - Generate test files: script to create random PDF/DOCX files
  - Monitor: perfmon (Windows Performance Monitor), dotnet-counters, custom metrics
  - Mock API: WireMock.Net with simulated latency and rate limiting
- SQLite performance:
  - Ensure WAL mode is enabled
  - Verify indexes on (status, next_attempt_at_utc)
  - Benchmark queries with 10k+ rows
- Document bottlenecks and recommendations for scaling (e.g., increase concurrency, optimize queries)

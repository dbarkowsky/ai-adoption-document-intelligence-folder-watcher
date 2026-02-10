# US-016: Health Monitoring and Metrics Collection

**As an** Operations Team member,
**I want to** monitor service health, backlog size, and delivery metrics in real-time,
**So that** I can detect issues proactively and ensure the service is operating within acceptable parameters.

## Acceptance Criteria
- [ ] **Scenario 1**: Health Check Endpoint or Periodic Heartbeat
    - **Given** the service is running
    - **When** a health check is requested or scheduled
    - **Then** the health check reports: DB connectivity OK, root path reachable, last scan time, backlog within thresholds

- [ ] **Scenario 2**: Track Discovered/Claimed/Sent/Failed Counts
    - **Given** files are being processed
    - **When** scan and send loops execute
    - **Then** metrics are emitted for: files discovered (count), files claimed (count), files sent (count), files failed (count), with per-minute or per-hour aggregation

- [ ] **Scenario 3**: Track Current Pending Jobs and Backlog Age
    - **Given** jobs are in the processing queue
    - **When** metrics are collected
    - **Then** the service reports: current pending jobs count (status in Claimed/RetryScheduled), oldest pending job age (time since discovered_at_utc)

- [ ] **Scenario 4**: Track API Success Rate and Latency
    - **Given** HTTP requests are being sent
    - **When** metrics are collected
    - **Then** the service reports: API success rate (%), average latency (ms), p50/p95/p99 latency percentiles, error rate (%)

- [ ] **Scenario 5**: Detect Stale Scan or Send Loops
    - **Given** the scan or send loop stops processing
    - **When** metrics are checked
    - **Then** an alert is triggered if last_scan_time or last_send_time exceeds a threshold (e.g., 2x ScanIntervalSeconds)

- [ ] **Scenario 6**: Expose Metrics for External Monitoring
    - **Given** metrics need to be consumed by monitoring tools
    - **When** metrics are emitted
    - **Then** metrics are available via Prometheus endpoint, Windows Performance Counters, or structured logs

- [ ] **Scenario 7**: Backlog Threshold Alerts
    - **Given** the backlog grows beyond a threshold
    - **When** metrics are evaluated
    - **Then** an alert is logged or sent if pending jobs count exceeds a configured limit or oldest job age exceeds a threshold

## Priority
- [ ] High (Must Have)
- [x] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Minimum metrics:
  - Counters: files_discovered, files_claimed, files_sent, files_failed
  - Gauges: pending_jobs_count, oldest_pending_job_age_seconds
  - Histograms: api_latency_ms, scan_duration_ms, send_duration_ms
  - Rates: api_success_rate, api_error_rate
- Health check criteria:
  - SQLite connectivity: attempt simple query (e.g., SELECT 1)
  - Root path reachability: check if Directory.Exists(<Root>)
  - Last scan time: verify scan loop is not stale
  - Backlog: pending jobs < threshold, oldest job age < threshold
- Consider using:
  - App.Metrics or prometheus-net for metrics
  - Health checks via ASP.NET Core health checks (optional HTTP endpoint)
  - Windows Performance Counters (optional)
- Emit metrics to structured logs if dedicated metrics system is not available
- Configuration:
  - BacklogThreshold: max pending jobs (default 1000)
  - OldestJobAgeThresholdHours: max job age before alert (default 24)

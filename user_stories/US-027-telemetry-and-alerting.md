# US-027: Telemetry and Alerting Integration

**As an** Operations Team member,
**I want to** integrate the service with monitoring and alerting systems,
**So that** I receive proactive alerts when issues occur and can track service health over time.

## Acceptance Criteria
- [ ] **Scenario 1**: Emit Metrics to Monitoring System
    - **Given** a monitoring system is available (Prometheus, Application Insights, etc.)
    - **When** the service is running
    - **Then** metrics are emitted for: files discovered/claimed/sent/failed, pending jobs count, API latency, scan duration, and send duration

- [ ] **Scenario 2**: Configure Alerts for Backlog Growth
    - **Given** the pending jobs count exceeds a threshold
    - **When** the alert is evaluated
    - **Then** an alert is triggered and sent to the operations team (email, Slack, PagerDuty)

- [ ] **Scenario 3**: Configure Alerts for Stale Processing
    - **Given** the oldest pending job age exceeds a threshold (e.g., 24 hours)
    - **When** the alert is evaluated
    - **Then** an alert is triggered indicating stale job processing

- [ ] **Scenario 4**: Configure Alerts for API Error Rate
    - **Given** the API error rate exceeds a threshold (e.g., 10% over 5 minutes)
    - **When** the alert is evaluated
    - **Then** an alert is triggered indicating API connectivity or availability issues

- [ ] **Scenario 5**: Configure Alerts for Service Health
    - **Given** the service health check fails (DB connectivity, root path unreachable, scan loop stale)
    - **When** the health check runs
    - **Then** an alert is triggered indicating service health degradation

- [ ] **Scenario 6**: Dashboard for Real-Time Monitoring
    - **Given** the operations team needs visibility into service performance
    - **When** they access the monitoring dashboard
    - **Then** the dashboard shows: pending jobs, files processed per hour, API latency percentiles, error rate, and service uptime

- [ ] **Scenario 7**: Track SLA Metrics
    - **Given** the service has SLA requirements (e.g., 99% delivery within 1 hour)
    - **When** metrics are collected
    - **Then** SLA metrics are calculated and tracked: delivery success rate, average delivery time, p95/p99 delivery time

- [ ] **Scenario 8**: Log Critical Events to External System
    - **Given** critical events occur (service start/stop, dead-letter, API failures)
    - **When** events are logged
    - **Then** events are sent to external logging system (Splunk, ELK, Azure Monitor) for aggregation and alerting

## Priority
- [ ] High (Must Have)
- [ ] Medium (Should Have)
- [x] Low (Nice to Have)

## Technical Notes / Assumptions
- Monitoring integrations:
  - Prometheus: use prometheus-net, expose /metrics endpoint
  - Application Insights: use Application Insights SDK
  - Windows Performance Counters: custom counters
  - Structured logs: emit metrics as log events for parsing
- Key metrics:
  - Counters: files_discovered, files_claimed, files_sent, files_failed
  - Gauges: pending_jobs_count, oldest_job_age_seconds
  - Histograms: api_latency_ms, scan_duration_ms, send_duration_ms
  - Rates: api_success_rate, api_error_rate
- Alerting thresholds (configurable):
  - Backlog: pending_jobs_count > 1000
  - Stale jobs: oldest_job_age > 24 hours
  - API error rate: > 10% over 5 minutes
  - Service health: health_check_status != OK
- Dashboard tools: Grafana, Azure Monitor, Power BI, custom web dashboard
- SLA metrics:
  - Delivery success rate: (sent / (sent + failed)) over time window
  - Average delivery time: time from discovered_at to sent_at
  - P95/P99 delivery time
- External logging:
  - Use Serilog sinks: Seq, Splunk, Elasticsearch, Azure Monitor
  - Ensure structured logs are emitted in JSON format

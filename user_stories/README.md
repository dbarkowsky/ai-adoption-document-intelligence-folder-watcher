# User Stories: Folder-to-API Delivery Service

FULL REQUIREMENTS DOCUMENT AT /REQUIREMENTS.md

Check of user stories as you complete them.

This directory contains all user stories for the Windows Server Folder-to-API Delivery Service project. User stories are organized into implementation phases to provide a logical development sequence.

## Implementation Status Tracking

Track progress by checking off completed stories in each phase.

---

## Phase 1: Foundation and Setup (Must Have - Week 1-2)

These stories establish the basic project structure and essential infrastructure.

- [x] **US-001**: [Project Setup and .NET Worker Service Foundation](US-001-project-setup-dotnet-worker-service.md)
  - Create .NET Worker Service project with Windows Service hosting
  - Configure Generic Host and dependency injection

- [x] **US-002**: [Configuration Management with Secure Secret Storage](US-002-configuration-management.md)
  - Define configuration schema with appsettings.json
  - Implement secure API key storage (Windows Credential Manager/DPAPI)

- [x] **US-003**: [Folder Structure Initialization and Validation](US-003-folder-structure-initialization.md)
  - Create and validate required folders (inbox, processing, sent, failed)
  - Support both local and UNC paths

- [x] **US-004**: [SQLite Database Setup with Schema Management](US-004-sqlite-database-setup.md)
  - Create SQLite database with jobs table
  - Enable WAL mode and create indexes
  - Implement schema migrations

---

## Phase 2: Core Data Layer (Must Have - Week 2-3)

Build the data access and job management foundation.

- [x] **US-005**: [Job Repository CRUD Operations with Transactional State Management](US-005-job-repository-crud-operations.md)
  - Implement all job lifecycle operations
  - Ensure transactional state transitions
  - Support querying due jobs

---

## Phase 3: File Discovery and Claiming (Must Have - Week 3-4)

Implement the file scanning, validation, and claiming pipeline.

- [x] **US-006**: [Inbox Enumeration and File Filtering](US-006-inbox-enumeration-and-filtering.md)
  - Scan inbox folder and apply eligibility filters
  - Support extension, size, and ignore pattern filtering

- [x] **US-007**: [File Completeness Checker with Exclusive Open](US-007-file-completeness-checker.md)
  - Implement exclusive open (FileShare.None) completeness check
  - Add stability age heuristic for SMB edge cases

- [x] **US-008**: [File Claiming Mechanism with Cross-Volume Support](US-008-file-claiming-mechanism.md)
  - Move files from inbox to processing
  - Handle cross-volume copy-verify-delete fallback

- [x] **US-009**: [Scan Loop Hosted Service with Periodic Scanning](US-009-scan-loop-hosted-service.md)
  - Implement periodic scan loop
  - Integrate enumeration, completeness check, and claiming
  - Ensure deterministic scanning (no duplicates)

---

## Phase 4: HTTP Delivery (Must Have - Week 4-5)

Build the HTTP client and delivery mechanism.

- [x] **US-010**: [HTTP Client Factory Setup with API Key Authentication](US-010-http-client-factory-setup.md)
  - Configure IHttpClientFactory with timeouts and auth
  - Add API key header to all requests

- [x] **US-011**: [API Client Implementation with Multipart Upload](US-011-api-client-implementation.md)
  - Implement multipart/form-data file upload
  - Handle success, retryable, and permanent errors
  - Log request/response details

---

## Phase 5: Retry and Resilience (Must Have - Week 5-6)

Implement retry logic and failure handling.

- [x] **US-012**: [Retry Policy and Exponential Backoff Strategy](US-012-retry-policy-and-backoff.md)
  - Calculate exponential backoff with jitter
  - Enforce max attempts and max job age
  - Determine retryable vs permanent errors

- [x] **US-013**: [Send Loop Hosted Service with Concurrent Delivery](US-013-send-loop-hosted-service.md)
  - Query and send due jobs with bounded concurrency
  - Update job state transactionally after send
  - Handle success, retry, and dead-letter cases

- [x] **US-014**: [Dead Letter Handling and Forensics](US-014-dead-letter-handling.md)
  - Move failed files to dead-letter folder
  - Preserve error details for troubleshooting
  - Log operator-facing error events

---

## Phase 6: Observability (Must Have - Week 6-7)

Add logging, monitoring, and operational visibility.

- [x] **US-015**: [Structured Logging with JSON Format and Log Rotation](US-015-structured-logging.md)
  - Implement structured logging with JSON output
  - Configure rolling log files and Windows Event Log
  - Redact sensitive data from logs

- [x] **US-017**: [Graceful Shutdown and State Consistency](US-017-graceful-shutdown.md)
  - Handle shutdown signals gracefully
  - Complete in-flight operations or checkpoint
  - Prevent SQLite corruption on exit

---

## Phase 7: Deployment and Operations (Must Have - Week 7-8)

Prepare for production deployment.

- [x] **US-019**: [Windows Service Deployment and Installation](US-019-windows-service-deployment.md)
  - Publish as single-file executable
  - Create installation and uninstall scripts
  - Configure service account, permissions, and recovery options

- [x] **US-024**: [Error Handling and Edge Case Coverage](US-024-error-handling-edge-cases.md)
  - Handle all edge cases gracefully
  - Validate configuration on startup
  - Handle file disappearance, path conflicts, oversized files

---

## Phase 8: Testing (Must Have - Week 8-10)

Comprehensive testing to ensure reliability.

- [x] **US-020**: [Unit Testing Framework with Mocked Dependencies](US-020-unit-testing-framework.md)
  - Create test project with xUnit and Moq
  - Mock filesystem, HTTP, and clock
  - Test all core components in isolation

- [ ] **US-021**: [Integration Testing with Real Dependencies](US-021-integration-testing.md)
  - Test end-to-end scenarios with real filesystem, SQLite, and HTTP
  - Test local and SMB paths
  - Verify locked file handling and retry behavior

- [ ] **US-022**: [Resilience Testing for Service Restarts and Network Outages](US-022-resilience-testing.md)
  - Test service restart, server reboot, and crash recovery
  - Verify network outage and API downtime handling
  - Ensure SQLite recovery and no data loss

---

## Phase 9: Optimization and Polish (Should Have - Week 10-11)

Additional features to improve operations and maintainability.

- [ ] **US-016**: [Health Monitoring and Metrics Collection](US-016-health-monitoring.md)
  - Implement health checks (DB, root path, scan/send loops)
  - Track metrics (pending jobs, API latency, success rate)
  - Expose metrics for external monitoring

- [ ] **US-018**: [Archive Retention and Cleanup for Sent and Failed Files](US-018-archive-retention-cleanup.md)
  - Implement automated cleanup for sent and failed folders
  - Configure retention policies (sent: 30 days, failed: 90 days)
  - Purge old job records from database

- [ ] **US-023**: [Load Testing for Sustained Throughput](US-023-load-testing.md)
  - Test 500 files/hour for sustained period
  - Measure CPU, memory, and throughput
  - Verify backlog stability under load

- [ ] **US-025**: [Security Hardening and Secret Management](US-025-security-hardening.md)
  - Secure API key storage and log redaction
  - Enforce HTTPS and validate TLS certificates
  - Set up least-privilege service account

- [x] **US-026**: [Comprehensive Documentation and Operations Guide](US-026-documentation.md)
  - Write installation, configuration, and operations guides
  - Document troubleshooting steps and FAQ
  - Create architecture diagrams and API integration guide

---

## Phase 10: Advanced Features (Nice to Have - Future)

Optional enhancements for advanced scenarios.

- [ ] **US-027**: [Telemetry and Alerting Integration](US-027-telemetry-and-alerting.md)
  - Integrate with Prometheus, Application Insights, etc.
  - Configure alerts for backlog, stale jobs, and API errors
  - Create monitoring dashboards

- [ ] **US-028**: [Future Enhancement - OAuth 2.0 Client Credentials Flow](US-028-future-oauth-support.md)
  - Design for OAuth token acquisition and refresh
  - Implement DelegatingHandler for token management

- [ ] **US-029**: [Future Enhancement - mTLS Client Certificate Authentication](US-029-future-mtls-support.md)
  - Support loading client certificates from Windows cert store
  - Configure SocketsHttpHandler for mTLS

- [ ] **US-030**: [Future Enhancement - Multi-Instance High Availability](US-030-future-multi-instance-ha.md)
  - Design for multi-instance deployment
  - Implement distributed locking and job partitioning
  - Migrate from SQLite to SQL Server/PostgreSQL

---

## Implementation Order Recommendation

### Week 1-2: Foundation
Start with Phase 1 (US-001 through US-004) to establish the basic project structure, configuration, folder layout, and database.

### Week 2-3: Data Layer
Complete Phase 2 (US-005) to build the job repository with all CRUD operations.

### Week 3-4: File Pipeline
Implement Phase 3 (US-006 through US-009) to build the file discovery, completeness checking, claiming, and scan loop.

### Week 4-5: HTTP Delivery
Complete Phase 4 (US-010, US-011) to implement the HTTP client and delivery mechanism.

### Week 5-6: Retry and Resilience
Implement Phase 5 (US-012 through US-014) to add retry logic, backoff, send loop, and dead-letter handling.

### Week 6-7: Observability
Add Phase 6 (US-015, US-017) for logging and graceful shutdown.

### Week 7-8: Deployment
Complete Phase 7 (US-019, US-024) for deployment tooling and edge case handling.

### Week 8-10: Testing
Execute Phase 8 (US-020 through US-022) for comprehensive unit, integration, and resilience testing.

### Week 10-11: Polish
Implement Phase 9 (US-016, US-018, US-023, US-025, US-026) for monitoring, cleanup, load testing, security, and documentation.

### Future: Advanced Features
Phase 10 (US-027 through US-030) can be implemented as needed based on evolving requirements.

---

## Story Dependencies

### Critical Path (Must implement in order)
1. US-001 → US-002 → US-003 → US-004 → US-005 (Foundation and data layer)
2. US-006 → US-007 → US-008 → US-009 (File pipeline)
3. US-010 → US-011 (HTTP client)
4. US-012 → US-013 → US-014 (Retry and send loop)

### Parallel Development Opportunities
- US-015 (Logging) can be implemented alongside any phase
- US-020 (Unit testing) should be developed in parallel with each component
- US-019 (Deployment) can be prepared while core features are being built
- US-024 (Error handling) should be integrated throughout development

### Independent Stories
- US-016 (Health monitoring) - can be added anytime after Phase 5
- US-018 (Archive cleanup) - can be added anytime after Phase 3
- US-025 (Security) - should be integrated throughout but can be hardened later
- US-026 (Documentation) - ongoing throughout project

---

## Story Statistics

- **Total Stories**: 30
- **Must Have (High Priority)**: 19 stories
- **Should Have (Medium Priority)**: 7 stories
- **Nice to Have (Low Priority)**: 4 stories

### By Phase
- **Phase 1 (Foundation)**: 4 stories
- **Phase 2 (Data Layer)**: 1 story
- **Phase 3 (File Pipeline)**: 4 stories
- **Phase 4 (HTTP Delivery)**: 2 stories
- **Phase 5 (Retry/Resilience)**: 3 stories
- **Phase 6 (Observability)**: 2 stories
- **Phase 7 (Deployment)**: 2 stories
- **Phase 8 (Testing)**: 3 stories
- **Phase 9 (Optimization)**: 5 stories
- **Phase 10 (Advanced)**: 4 stories

---

## Quick Reference

### Core Components User Stories
- **Configuration**: US-002
- **Database**: US-004, US-005
- **File Scanning**: US-006, US-007, US-008, US-009
- **HTTP Delivery**: US-010, US-011, US-012, US-013
- **Error Handling**: US-014, US-024
- **Logging**: US-015
- **Testing**: US-020, US-021, US-022, US-023
- **Deployment**: US-019

### Operational User Stories
- **Monitoring**: US-016, US-027
- **Maintenance**: US-018
- **Security**: US-025
- **Documentation**: US-026

### Future Enhancements
- **OAuth 2.0**: US-028
- **mTLS**: US-029
- **High Availability**: US-030

---

## Notes for AI Implementation

When implementing these user stories:

1. **Start with Foundation**: Complete Phase 1 (US-001 to US-004) before moving forward
2. **Test as You Go**: Implement US-020 (unit testing) alongside each component
3. **Follow Dependencies**: Respect the critical path dependencies listed above
4. **Iterate on Core Loop**: Get US-009 (scan loop) and US-013 (send loop) working end-to-end early
5. **Security First**: Integrate US-025 security considerations from the beginning
6. **Document Continuously**: Update US-026 documentation as features are completed

### Acceptance Criteria Checklist
Each user story contains detailed acceptance criteria with Given/When/Then scenarios. Mark each scenario as complete when:
- Code is written and reviewed
- Unit tests pass (where applicable)
- Integration tests pass (where applicable)
- Documentation is updated
- Code is merged to main branch

### Definition of Done
A user story is complete when:
- [ ] All acceptance criteria scenarios are passing
- [ ] Unit tests written and passing (80%+ coverage for core logic)
- [ ] Integration tests written and passing (where applicable)
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] No critical or high-severity bugs
- [ ] Deployed to test environment and validated

---

## Contact and Questions

For questions about user stories or implementation guidance, refer to:
- Original requirements document: `2026-02-09 folder watcher requirements.md`
- Architecture appendix in requirements (Section A)
- Technical notes in each user story file

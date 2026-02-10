# US-030: Future Enhancement - Multi-Instance High Availability

**As a** DevOps Engineer,
**I want to** design for future multi-instance deployment with distributed locking,
**So that** the service can be scaled horizontally for high availability and increased throughput.

## Acceptance Criteria
- [ ] **Scenario 1**: Document Multi-Instance Architecture
    - **Given** HA support is planned for a future version
    - **When** architecture is documented
    - **Then** the design covers: shared database (SQL Server/PostgreSQL), distributed locking mechanism, active-active vs active-passive, and job partitioning strategy

- [ ] **Scenario 2**: Evaluate Database Options for Multi-Instance
    - **Given** SQLite doesn't support multi-instance coordination
    - **When** HA requirements are evaluated
    - **Then** migration path to SQL Server or PostgreSQL is documented with schema migration and connection pooling considerations

- [ ] **Scenario 3**: Design Distributed Locking Strategy
    - **Given** multiple instances must not claim the same file
    - **When** distributed locking is designed
    - **Then** options are evaluated: database-based locking (pessimistic row locks), Redis locks, or Azure Blob leases

- [ ] **Scenario 4**: Implement Instance Heartbeat
    - **Given** multiple instances are running
    - **When** heartbeat mechanism is implemented
    - **Then** each instance registers itself in a shared registry with last_heartbeat_utc, and stale instances can be detected and cleaned up

- [ ] **Scenario 5**: Handle Job Ownership Transfer
    - **Given** an instance crashes mid-processing
    - **When** another instance detects the stale ownership
    - **Then** orphaned jobs are reclaimed and processing resumes on a healthy instance

- [ ] **Scenario 6**: Partition Work Across Instances
    - **Given** multiple instances are scanning the same inbox
    - **When** work partitioning is implemented
    - **Then** jobs are partitioned by hash(job_id) % instance_count or by assigned instance_id to prevent duplicate claiming

## Priority
- [ ] High (Must Have)
- [ ] Medium (Should Have)
- [x] Low (Nice to Have)

## Technical Notes / Assumptions
- Multi-instance considerations:
  - Shared state: requires SQL Server, PostgreSQL, or cloud database
  - Distributed locking: pessimistic locks, Redis, Azure Blob leases
  - Job partitioning: hash-based or instance assignment
  - Heartbeat: periodic registration in shared registry
  - Orphaned job recovery: detect stale ownership and reclaim
- Architecture options:
  - Active-active: all instances scan and send concurrently (requires coordination)
  - Active-passive: one active scanner, others are hot standbys (simpler but lower throughput)
- Database migration:
  - SQLite → SQL Server/PostgreSQL
  - Add columns: instance_id, locked_by, locked_at_utc
  - Use pessimistic locks: SELECT FOR UPDATE SKIP LOCKED
- Locking strategies:
  - Database: SELECT FOR UPDATE (PostgreSQL), UPDLOCK/ROWLOCK (SQL Server)
  - Redis: SET NX EX for distributed locks
  - Azure Blob: lease blobs for coordination
- v1 scope: single instance only
- Future enhancement: explicitly planned but deferred
- Document in architecture guide for reference

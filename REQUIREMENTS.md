## Environment prerequisites
Windows Server host(s) for deployment (and a separate dev/build machine or the same server if you must).

.NET installed: for development you need the .NET SDK; for running the service you need the .NET runtime (or publish self-contained so no runtime install is needed).

If you plan to follow Microsoft’s common Windows Service publish approach, publish an .exe (Worker Service) and install it via Service Control Manager (sc.exe).

## Why Windows Service (vs Power Automate)

Power Automate’s File System connector (via the On-premises Data Gateway) has trigger behaviors and limits that make “guaranteed delivery” harder to prove end-to-end without additional compensating design (durable ledger, deterministic re-scan, backlog control).[^1]
Specifically, the connector documents that file triggers can delay returning files and don’t guarantee returning all files in a single run in some configurations, triggers don’t fire for subfolder changes, and there are throttling and size limits that can complicate burst handling and deterministic processing.[^1]
Also, event-driven file watching (e.g., `FileSystemWatcher`) can lose track of changes if its internal buffer overflows during bursts, which is one reason this design uses periodic scanning only.[^2]


# Technical Requirements: Windows Server Folder-to-API Delivery Service
Version: v1.0  
Date: 2026-02-09  
Owner: <Your Team/Org>  
Status: Draft for implementation

## 1. Purpose
Build a Windows Server solution that reliably scans a folder (local NTFS path or SMB UNC share) for documents and delivers them to a remote HTTP API with guaranteed delivery (eventual delivery) semantics.

This document is intended to be provided to an implementation AI/engineering team to build the solution.

## 2. Scope

### 2.1 In scope
- Periodic scanning (polling) of a configured folder for new documents.
- Safe handling of partially-written/locked files using **exclusive-open checks**.
- Durable state tracking using a local embedded database (**SQLite**).
- Reliable HTTP delivery with retries/backoff until success.
- File lifecycle management (inbox → processing → sent/failed).
- Operational logging and health/monitoring hooks.

### 2.2 Out of scope (v1)
- OAuth 2.0, client certificates (mTLS), advanced secret rotation (documented as future enhancement).
- Client-side idempotency keys (duplicates handled by the server).
- Complex content transformations, OCR, indexing (unless explicitly added later).
- Multi-node active-active scaling (single service instance per watched root for v1).

## 3. Definitions
- **Guaranteed delivery / eventual delivery**: If the service is running and the remote API becomes reachable, every eligible file will eventually be delivered successfully or moved to a dead-letter (“failed”) state with a recorded reason.
- **Eligible file**: A file that matches configured extension/size rules and passes completeness checks.
- **Completeness check**: File is considered complete when it can be opened with `FileShare.None` for read (exclusive open). If locked, it is not processed yet.

## 4. Assumptions and constraints
- File sizes: up to a few MB.
- Throughput: up to hundreds of files per hour.
- Source folder location: local disk OR SMB share (UNC path).
- API auth for v1: **API key header**.
- Service must tolerate: API downtime, network interruption, service restarts, server reboots.

## 5. High-level architecture (store-and-forward)
The solution is a deterministic store-and-forward pipeline:

1) Periodic scanner enumerates `<Root>\inbox`  
2) For each candidate file:
   - Run completeness check (exclusive open); if locked, skip until later
   - Claim the file by moving it into `<Root>\processing`
   - Create/update a durable job row in SQLite
3) Sender worker uploads file to remote API (API key header)
4) On success: move file to `<Root>\sent` and mark job `Sent`
5) On failure: record error, schedule next retry time, and keep the file in processing
6) On permanent failure / too many attempts: move file to `<Root>\failed`

## 6. Folder layout and lifecycle

### 6.1 Root layout
Configured root: `<Root>`

- `<Root>\inbox`        (producers drop files here)
- `<Root>\processing`   (service-owned working area)
- `<Root>\sent`         (archive after success)
- `<Root>\failed`       (dead-letter for permanent failures)

### 6.2 File state machine (conceptual)
- Discovered → (Locked) → Discovered
- Discovered → Claimed → Sending → Sent
- Sending → RetryScheduled → Sending
- Sending → Failed (dead-letter)

### 6.3 Retention
- `<Root>\sent`: retain N days (configurable), then purge.
- `<Root>\failed`: retain longer (configurable), requires manual review.

## 7. Periodic scan requirements (no FileSystemWatcher)

### 7.1 Scanner schedule
- The service MUST use periodic scanning only.
- `ScanIntervalSeconds` default: 30 (configurable range 10–120).

### 7.2 Enumeration rules
- Enumerate files directly under `<Root>\inbox` (non-recursive by default).
- Optional: recursive scanning can be enabled, but must be explicit because it changes performance/semantics.

### 7.3 Eligibility filters
Configurable:
- Allowed extensions (e.g., `.pdf`, `.docx`, `.tif`).
- Maximum size MB (guardrail).
- Ignore patterns (e.g., filenames starting with `~`, hidden/system files).

### 7.4 Determinism
- Scanning MUST be deterministic: repeated scans must not create duplicates or lose jobs.
- If SQLite contains a job for a file already claimed/processed, the scanner must not re-claim it.

## 8. File completeness check (exclusive open only)

### 8.1 Required behavior
A file is “ready” only if the service can open it for read with **no sharing**:

- Attempt: `FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)`
- If success: close immediately, continue to claim.
- If fails due to lock/sharing violation: treat as “Locked”, skip for now, try again next scan.
- Do not spin-wait; rely on scan schedule.

### 8.2 Optional stability heuristic (recommended for SMB edge cases)
To reduce repeated lock attempts:
- Require `LastWriteTimeUtc` to be older than `StableAgeSeconds` (default 5–10 seconds) before attempting exclusive open.

## 9. Claiming, concurrency, and ownership

### 9.1 Single-instance rule (v1)
- v1 assumes exactly one service instance processes a given `<Root>` at a time.

### 9.2 Claim operation
Preferred claim mechanism:
- Move file from `<Root>\inbox\<name>` to `<Root>\processing\<jobId>\<name>`.
- Create `<Root>\processing\<jobId>\` if needed.
- After claim, the file is service-owned; producers must not modify files in processing.

Handling failures:
- If move fails due to transient errors: record and retry later.
- If move fails because file disappeared: record as “Gone” and continue.

Cross-volume note:
- If inbox and processing are on different volumes/shares, a “move” may degrade to copy+delete.
- If cross-volume is unavoidable, implementation MUST:
  - Copy to destination temp name
  - Verify size (and optionally checksum)
  - Delete source only after verification
  - Record state transitions transactionally in SQLite to avoid duplicates/loss

## 10. Durable job ledger (SQLite)

### 10.1 Decision
- v1 MUST use SQLite as the durable job/state store.

### 10.2 SQLite operational requirements
- Store the DB on local disk (not on SMB) for reliability.
- Enable WAL mode.
- All job state transitions must occur inside transactions.

### 10.3 Minimum schema (conceptual)
Table: `jobs`
- `job_id` TEXT PRIMARY KEY
- `source_path` TEXT NOT NULL            (original inbox path)
- `processing_path` TEXT                (claimed path)
- `status` TEXT NOT NULL                (Discovered/Claimed/Sending/RetryScheduled/Sent/Failed)
- `discovered_at_utc` TEXT NOT NULL
- `claimed_at_utc` TEXT
- `sent_at_utc` TEXT
- `attempt_count` INTEGER NOT NULL DEFAULT 0
- `next_attempt_at_utc` TEXT
- `last_error` TEXT
- `last_http_status` INTEGER
- `file_size_bytes` INTEGER
- `file_mtime_utc` TEXT

Indexes:
- `(status, next_attempt_at_utc)`
- `(processing_path)` unique if desired

### 10.4 Other DB options (documented, not selected for v1)
- SQL Server: better for multi-instance coordination and central ops; adds dependency and administration overhead.
- PostgreSQL: similar tradeoffs to SQL Server in this context.
- No-DB (filesystem only): not acceptable for guaranteed delivery because state can be ambiguous after crashes.

## 11. Delivery worker (HTTP) and retry policy

### 11.1 HTTP request requirements
- Method: POST
- URL: `EndpointUrl` (HTTPS required)
- Auth: header `X-API-Key: <apiKey>` (header name configurable)
- Payload: configurable:
  - Option A (preferred): multipart/form-data with fields:
    - `file` (binary)
    - `filename` (string)
    - optional metadata fields (timestamp, source system)
  - Option B: raw binary body with content-type derived from extension

Timeouts (configurable):
- Connect timeout default 10s
- Overall request timeout default 60s

### 11.2 Retry conditions
Retry on:
- Network failures (DNS, connect, TLS, timeouts)
- HTTP 408, 429 (optional), and 5xx

Do not retry (dead-letter) on:
- Most 4xx indicating permanent failure (e.g., 400/401/403/404/415), unless explicitly allowlisted

### 11.3 Backoff schedule
- Exponential backoff with jitter, capped (configurable).
Example (illustrative):
- 10s, 30s, 2m, 5m, 15m, 30m, 60m (cap)

### 11.4 Delivery completion
- Only mark `Sent` after receiving a success response (2xx; configurable list).
- After `Sent`, move file to `<Root>\sent`.

### 11.5 Duplicate handling
- Client does not implement idempotency keys in v1.
- Server-side must tolerate duplicates caused by retries or restarts.

## 12. Failure handling and dead-letter

### 12.1 Dead-letter rules
Move to `<Root>\failed` and mark job `Failed` when:
- `attempt_count` exceeds `MaxAttempts` (configurable), OR
- job age exceeds `MaxJobAgeDays` (configurable), OR
- response is a permanent non-retryable error

### 12.2 Forensics
When a job fails permanently:
- Preserve file in failed folder
- Preserve error reason and last HTTP status in SQLite
- Log an operator-facing error event

## 13. Service hosting and deployment

### 13.1 Windows Service requirements
- Runs as Windows Service (auto-start).
- Graceful shutdown: stop taking new work; finish current send or checkpoint quickly; never corrupt SQLite.

### 13.2 Service account and permissions
- Dedicated least-privilege account.
- Access:
  - Read/write/modify: `<Root>\inbox`, `<Root>\processing`, `<Root>\sent`, `<Root>\failed`
  - Read/write: local SQLite DB directory
  - Network access to API endpoint

### 13.3 Installer
- Provide a repeatable install:
  - Create folders
  - Register service
  - Set recovery options (restart on failure)
  - Validate config

## 14. Configuration

### 14.1 Required config keys
- `RootPath`
- `EndpointUrl`
- `ApiKey` (stored securely; config references secret location)
- `ScanIntervalSeconds`
- `StableAgeSeconds`
- `AllowedExtensions`
- `MaxFileSizeMB`
- `MaxAttempts` and/or `MaxJobAgeDays`
- Retry/backoff parameters
- Archive retention settings

### 14.2 Secret storage
- API key must not be stored in plaintext in logs.
- Prefer Windows Credential Manager or DPAPI-protected config.

## 15. Observability

### 15.1 Logs
- Structured logs (JSON) with at least:
  - `job_id`, `filename`, `status`, `attempt_count`, `error`, `http_status`, `latency_ms`
- Rolling log files and/or Windows Event Log.

### 15.2 Metrics (minimum)
- Discovered/claimed/sent/failed counts per minute
- Current pending jobs and oldest pending age
- API success rate and latency percentiles

### 15.3 Health
- Health endpoint or periodic “heartbeat” event:
  - DB connectivity OK
  - Root path reachable
  - Last scan time
  - Backlog within thresholds

## 16. Testing requirements

### 16.1 Functional tests
- Local disk: deliver N files successfully.
- SMB: deliver N files successfully.
- Locked file: ensure service waits until writer releases lock.
- Retry: simulate 5xx/timeouts; ensure eventual success.
- Permanent 4xx: ensure dead-letter.

### 16.2 Resilience tests
- Restart service mid-send: job resumes correctly.
- Reboot server: resumes.
- SMB outage: scanner recovers without losing jobs.

### 16.3 Load tests
- 500 files/hour with typical size (2–5 MB) for sustained period.
- Verify CPU/memory bounds and backlog stability.

## 17. Options considered and discarded (with rationale)

### 17.1 Power Automate + On-premises Data Gateway + File System connector (discarded for v1)
Why considered:
- Fast to implement, managed connectors, built-in HTTP actions.

Why discarded for v1 guaranteed delivery:
- Harder to guarantee deterministic “scan/claim” semantics and durable, queryable state without building additional storage and compensating logic.
- Connector trigger behavior/limitations can delay or omit returning all files in a single run in some configurations; subfolder triggering requires multiple triggers; throttling limits can constrain bursts.

### 17.2 FileSystemWatcher (discarded)
Why considered:
- Low latency.

Why discarded:
- Not required given your throughput.
- Adds complexity and can miss events under burst conditions; periodic scan is simpler and deterministic.

### 17.3 Database options
- SQLite (chosen): simplest embedded ledger, good enough for single node.
- SQL Server/PostgreSQL (not chosen): better multi-instance coordination; adds dependency/admin.
- Queue-based (not chosen for v1): RabbitMQ/MSMQ/Azure Service Bus; adds infra and changes ingestion model.

## 18. Future enhancements (explicitly planned)
- OAuth 2.0 client credentials, token caching/refresh.
- mTLS/client certificate auth from Windows cert store.
- Optional client idempotency key support if server requirements change.
- Multi-instance/HA with distributed locking and shared DB.
- Content hashing, checksum verification for cross-volume copy flows.

# Appendix A — Technology choice and .NET architecture (selected)

## A1. Decision: implement as a .NET Worker Service running as a Windows Service
We will implement this solution in .NET using the Worker Service template (Generic Host + hosted services) because it is designed for long-running background processes and is “logging, configuration, and dependency injection (DI) ready.” [page:3]  
A .NET Worker Service provides a standard structure (Program + Worker/BackgroundService) and is intended for scheduled/time-based operations and long-running service workloads. [page:3]  

We will run the Worker Service as a Windows Service using the Microsoft-supported integration (`Microsoft.Extensions.Hosting.WindowsServices` and `AddWindowsService`), which configures the host to work as a Windows Service and supports standard Windows service operations and Event Log visibility. [page:4]  
Microsoft’s Windows Service guidance also documents service publishing and installation patterns (including publishing an `.exe` and creating the service using `sc.exe create`). [page:4]  

### Why not Power Automate + On-premises Data Gateway (for this project)
Power Automate’s File System connector documents trigger/behavior constraints (for example, subfolder triggering and trigger-return behavior), and it also documents throttling and file-size limits that can complicate deterministic “guaranteed delivery” designs without additional compensating storage and reconciliation logic. [page:1]  
Because this project’s core requirement is guaranteed eventual delivery with a durable, queryable ledger and deterministic processing semantics, we are selecting a dedicated Windows Service application where scan/claim/send/retry behavior is fully controlled and testable. [page:1][page:4]  

## A2. Recommended .NET stack and packages
Target framework: `.NET 8` (or later) on Windows Server, implemented as a Worker Service. [page:4][page:3]  
Windows Service integration package: `Microsoft.Extensions.Hosting.WindowsServices` (required to interop with native Windows Services from .NET hosted services). [page:4]  

HTTP client management: use `IHttpClientFactory` (via `AddHttpClient`) to integrate `HttpClient` with DI, logging, and centralized configuration, while also managing handler lifetime and avoiding common DNS/handler lifetime pitfalls. [page:5]  
SQLite provider: use `Microsoft.Data.Sqlite`, which Microsoft documents as a lightweight ADO.NET provider for SQLite and usable independently of EF Core. [page:5]  

## A3. Process model: components and responsibilities
Implement the service as a small set of single-purpose components wired together with DI (all components must be unit-testable with mocked filesystem and HTTP). [page:3][page:5]

**Hosted services / long-running loops**
1. `ScanLoopHostedService`  
   - Every `ScanIntervalSeconds`, enumerates `<Root>\inbox` and identifies eligible files.  
   - For each file, calls `CompletenessChecker` (exclusive-open check) and, if complete, calls `Claimer` and records a job in `JobRepository` (SQLite).  
   - MUST be deterministic and safe to run repeatedly.  

2. `SendLoopHostedService`  
   - Continuously reads “due” jobs from SQLite (`status in {Claimed, RetryScheduled}` and `next_attempt_at <= now`).  
   - Sends the file to the API (API-key header) using an `HttpClient` created via `IHttpClientFactory`. [page:5]  
   - Updates job state transactionally and moves files to `sent` or `failed`.  

**Core services**
- `InboxEnumerator`  
  - Enumerates files; applies extension/size/ignore filters.

- `CompletenessChecker` (exclusive-open only)  
  - Attempts to open file with `FileShare.None`; if it fails due to lock/sharing violations, the file is skipped until a later scan.  

- `Claimer`  
  - Moves a file from `inbox` to `processing\<jobId>\` and returns the new path.  
  - Must handle move/copy fallback if inbox and processing are not on the same volume/share.

- `JobRepository` (SQLite)  
  - Creates/updates jobs and provides “dequeue due jobs” queries.  
  - All state transitions must be transactional.

- `ApiClient` (typed or named HttpClient wrapper)  
  - Adds `X-API-Key` (configurable header name) per request.  
  - Uses `HttpClient` from `IHttpClientFactory`. [page:5]

- `RetryPolicy`  
  - Computes backoff and sets `next_attempt_at`.  

**Cross-cutting**
- `Clock` abstraction for testable time.
- Structured logging (rolling files and/or Windows Event Log). [page:4]

## A4. Program.cs and hosting model (implementation guidance)
Use the Worker Service template and the Generic Host, which registers hosted services via `AddHostedService` and runs them under `IHost`. [page:3]  
Enable Windows Service hosting by calling `AddWindowsService(...)`, which configures the app to work as a Windows Service (including setting a ServiceName). [page:4]

Suggested DI registrations:
- `AddHostedService<ScanLoopHostedService>()` and `AddHostedService<SendLoopHostedService>()` (separate loops so scans don’t block sending).  
- `AddHttpClient("RemoteApi", ...)` to configure base address, default headers, and timeouts, and then inject `IHttpClientFactory` into `ApiClient`. [page:5]  
- Register `JobRepository` as a singleton that owns SQLite connection creation policy, while ensuring each operation uses its own connection/transaction as appropriate.

### BackgroundService exception behavior (important for Windows Service recovery)
Microsoft documents that .NET hosting behavior (BackgroundServiceExceptionBehavior) affects whether an unhandled exception stops the host and may prevent the Windows Service Control Manager from restarting the service unless the process terminates with a non-zero exit code. [page:4]  
Implementation requirement: in each hosted loop, catch unexpected exceptions, log, and fail fast (terminate process) so Windows Service recovery options can restart the service. [page:4]  

## A5. HTTP implementation details (API key now; OAuth/mTLS later)
Use `IHttpClientFactory` so `HttpClient` is DI-ready, centrally configured, and handler lifetimes are managed to reduce resource issues and DNS-staleness problems. [page:5]  
Microsoft documents that `IHttpClientFactory` manages caching and lifetime of underlying handlers and that clients created by the factory are intended to be short-lived, while handler lifetime is configurable (default handler lifetime is two minutes). [page:5]  

v1 auth requirement:
- Add header `X-API-Key: <value>` (header name configurable) to every request.

Future auth enhancements (explicitly deferred, but design for it):
- OAuth 2.0 client credentials: implement a `DelegatingHandler` that adds `Authorization: Bearer <token>` and refreshes tokens.
- Client certificates (mTLS): configure the primary handler via `ConfigurePrimaryHttpMessageHandler` (or `UseSocketsHttpHandler`) and load certs from Windows cert store, which is supported by the IHttpClientFactory handler configuration model. [page:5]  

## A6. SQLite implementation details
Use `Microsoft.Data.Sqlite` for SQLite access, which implements common ADO.NET abstractions for connections/commands/readers and is installed via NuGet (`dotnet add package Microsoft.Data.Sqlite`). [page:5]  
Store the SQLite file on local disk (not SMB) and enable WAL mode to improve concurrency characteristics for multiple threads/loops (scan + send).  

Implementation requirements:
- On service start, ensure database exists, apply migrations (simple version table).
- Use transactions for state transitions such as:
  - Create job + claim path update
  - Mark Sending + increment attempt_count
  - Record response + schedule retry
  - Mark Sent/Failed + final move

## A7. Concurrency and flow control
Use bounded concurrency for sends (e.g., a fixed number of parallel uploads) to avoid overloading the API and to maintain predictable resource use.  
Keep scanning single-threaded or lightly parallelized; claim operations should be serialized per file to avoid duplicate claiming in v1.

Recommended internal pattern:
- The send loop queries “due jobs,” then processes them with a `SemaphoreSlim`-controlled degree of parallelism.
- All SQLite updates for a job must be serialized (row-level by job_id via application logic).

## A8. Deployment: publish and install as a Windows Service
Microsoft recommends publishing the worker app as a single-file executable for Windows Service deployment because it is less error-prone than deploying many dependent files. [page:4]  
Install and manage the service with `sc.exe create`, `sc.exe start`, and `sc.exe stop`, which Microsoft documents as the native approach via the Windows Service Control Manager. [page:4]  

Minimum deployment steps:
1. `dotnet publish -c Release -r win-x64` with single-file enabled.
2. Copy published folder to `C:\Program Files\<Company>\<Service>\`.
3. Create service:
   - `sc.exe create "<ServiceName>" binpath= "C:\Program Files\<Company>\<Service>\<Service>.exe"`
4. Configure recovery (restart on failure) using `sc.exe failure ...` per your policy. [page:4]
5. Grant NTFS/SMB permissions to the service account for `<Root>` and to the local SQLite directory.

## A9. Detailed “build plan” checklist (implementation instructions)
1. Create project
   - `dotnet new worker --name FolderToApi.Service` (Worker Service template). [page:3][page:4]
2. Add packages
   - `Microsoft.Extensions.Hosting.WindowsServices` (Windows Service integration). [page:4]
   - `Microsoft.Data.Sqlite` (SQLite provider). [page:5]
3. Configuration
   - Add `appsettings.json` keys: RootPath, EndpointUrl, ApiKey, ScanIntervalSeconds, StableAgeSeconds, AllowedExtensions, MaxSizeMB, Retry policy knobs.
4. Database
   - Implement migrations (schema version table).
   - Implement JobRepository CRUD + “get due jobs” query.
5. Scanning
   - Implement periodic scan using a timer/PeriodicTimer.
   - Enumerate inbox files; apply eligibility filters.
   - For each file: if (StableAgeSeconds satisfied) attempt exclusive-open; if locked skip; else claim + create job row.
6. Sending
   - Register HttpClient via AddHttpClient and inject IHttpClientFactory. [page:5]
   - Build ApiClient: POST multipart or stream; add API key header.
   - Implement retry rules + backoff scheduling in SQLite.
7. File moves
   - Implement atomic-ish move where possible; implement safe copy-verify-delete fallback for cross-volume.
8. Logging/telemetry
   - Emit structured logs with job_id, filename, status, attempts, http_status, latency.
   - Optional: Event Log provider as per Windows Service guidance. [page:4]
9. Recovery semantics
   - Ensure unhandled exceptions terminate the process so SCM recovery can restart service. [page:4]
10. Test matrix
   - Local + SMB; locked files; API down; reboot/restart; throughput test.

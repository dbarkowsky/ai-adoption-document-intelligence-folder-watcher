# Architecture and Design Documentation

Comprehensive architecture documentation for the Folder-to-API Delivery Service.

## Table of Contents

1. [System Overview](#system-overview)
2. [Folder Layout and File Lifecycle](#folder-layout-and-file-lifecycle)
3. [Component Architecture](#component-architecture)
4. [State Machine](#state-machine)
5. [Database Schema](#database-schema)
6. [Process Flows](#process-flows)
7. [Concurrency and Threading](#concurrency-and-threading)
8. [Error Handling and Retry Strategy](#error-handling-and-retry-strategy)
9. [Deployment Architecture](#deployment-architecture)
10. [Design Decisions](#design-decisions)

---

## System Overview

The Folder-to-API Delivery Service is a store-and-forward file delivery system built on .NET Worker Service framework. It provides guaranteed eventual delivery semantics for files uploaded to a remote HTTP API.

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Folder-to-API Service                      │
│                                                             │
│  ┌──────────────────┐              ┌───────────────────┐   │
│  │  ScanLoop        │──────────────>│  SQLite Database  │   │
│  │  HostedService   │   Create Job  │  (Job Ledger)     │   │
│  └──────────────────┘              └───────────────────┘   │
│          │                                    │             │
│          │ Enumerate & Claim                  │ Query Due  │
│          ▼                                    ▼             │
│  ┌──────────────────┐              ┌───────────────────┐   │
│  │  inbox/          │              │  SendLoop         │   │
│  │  processing/     │              │  HostedService    │   │
│  │  sent/           │              └───────────────────┘   │
│  │  failed/         │                        │             │
│  └──────────────────┘                        │ HTTP POST   │
│                                               ▼             │
└───────────────────────────────────────────────┼─────────────┘
                                                │
                                                ▼
                                      ┌──────────────────┐
                                      │   Remote API     │
                                      │   (HTTPS)        │
                                      └──────────────────┘
```

### Key Design Principles

1. **Deterministic Processing**: No file is processed more than once; scan/claim operations are idempotent
2. **Durable State**: All state persisted in SQLite before acknowledging file processing
3. **Separation of Concerns**: Scan loop and send loop run independently for resilience
4. **Graceful Degradation**: Failures in one component don't crash the entire service
5. **Observable**: Comprehensive structured logging for diagnostics

---

## Folder Layout and File Lifecycle

### Folder Structure

```
<RootPath>/
├── inbox/                  # Files arrive here (producers write)
│   ├── document1.pdf
│   └── document2.docx
│
├── processing/             # Service working directory (service owns)
│   ├── <job-id-1>/
│   │   └── document1.pdf
│   └── <job-id-2>/
│       └── document2.docx
│
├── sent/                   # Successfully delivered files (archive)
│   ├── document1.pdf
│   └── document3.docx
│
└── failed/                 # Permanently failed files (dead-letter)
    └── document-error.pdf
```

### File Lifecycle Diagram

```
┌─────────────┐
│  Producer   │
│  Writes     │
│  to inbox/  │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────────────────────────────────┐
│                     SERVICE BEGINS HERE                      │
└─────────────────────────────────────────────────────────────┘
       │
       ▼
┌──────────────────┐
│  File in inbox/  │
└────────┬─────────┘
         │
         │ ScanLoop discovers file
         ▼
    ┌────────┐
    │ Locked?│───Yes──> Skip, retry next scan
    └───┬────┘
        │ No
        ▼
    ┌─────────────┐
    │ Eligible?   │───No──> Skip permanently (log)
    │ (ext, size) │
    └──────┬──────┘
           │ Yes
           ▼
    ┌──────────────────┐
    │ Claim File       │
    │ Move to          │
    │ processing/<id>/ │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │ Create Job in DB │
    │ State: Claimed   │
    └────────┬─────────┘
             │
             │ SendLoop picks up job
             ▼
    ┌──────────────────┐
    │ Attempt Upload   │
    │ to Remote API    │
    └────────┬─────────┘
             │
        ┌────┴────┐
        │         │
   Success      Failure
        │         │
        ▼         ▼
┌──────────┐  ┌────────────────┐
│Move to   │  │ Retryable?     │
│sent/     │  └────┬───────┬───┘
│State:Sent│       │       │
└──────────┘      Yes     No
                   │       │
                   ▼       ▼
          ┌───────────┐  ┌────────────┐
          │Schedule   │  │Move to     │
          │Next Retry │  │failed/     │
          │State:     │  │State:Failed│
          │RetryPend  │  └────────────┘
          └─────┬─────┘
                │
                │ (After backoff delay)
                └──> Retry upload (loop back to "Attempt Upload")
```

### State Transitions

Files progress through these states:

1. **Discovered** (in inbox): File found, waiting for completeness check
2. **Claimed** (in processing): File moved to processing, job created in DB
3. **Sending**: First upload attempt in progress
4. **RetryScheduled**: Upload failed, waiting for next retry time
5. **Sent** (in sent): Upload successful, archived
6. **Failed** (in failed): Permanent failure, moved to dead-letter

---

## Component Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        Program.cs                           │
│  (Generic Host + DI Container + WindowsService Integration) │
└────┬────────────────────────────────────────────────────┬───┘
     │                                                    │
     │ Registers                                          │ Registers
     ▼                                                    ▼
┌──────────────────────┐                     ┌──────────────────────┐
│ ScanLoopHostedService│                     │ SendLoopHostedService│
└──────┬───────────────┘                     └──────┬───────────────┘
       │ Uses                                       │ Uses
       ▼                                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Core Services (DI)                         │
├─────────────────────────────────────────────────────────────┤
│ • InboxEnumerator      (scans inbox)                        │
│ • CompletenessChecker  (exclusive-open test)                │
│ • FileClaimer          (moves files to processing)          │
│ • JobRepository        (SQLite CRUD operations)             │
│ • ApiClient            (HTTP file upload)                   │
│ • RetryPolicy          (calculates backoff)                 │
│ • Clock                (time abstraction for testing)       │
└─────────────────────────────────────────────────────────────┘
       │                                            │
       │ Reads/Writes                              │ HTTP POST
       ▼                                            ▼
┌────────────────┐                          ┌────────────────┐
│  File System   │                          │  Remote API    │
│  (Folders)     │                          │  (HTTPS)       │
└────────────────┘                          └────────────────┘
       │
       │ Persists State
       ▼
┌────────────────┐
│  SQLite DB     │
│  (jobs table)  │
└────────────────┘
```

### Core Components

#### 1. ScanLoopHostedService

**Purpose:** Periodically scans inbox folder for new files and creates jobs.

**Responsibilities:**
- Run scan every `ScanIntervalSeconds`
- Enumerate files in inbox using `InboxEnumerator`
- Check file completeness using `CompletenessChecker`
- Claim eligible files using `FileClaimer`
- Create job records in database using `JobRepository`
- Handle scan loop exceptions gracefully

**Threading:** Single background thread (BackgroundService)

---

#### 2. SendLoopHostedService

**Purpose:** Continuously processes due jobs and uploads files to API.

**Responsibilities:**
- Query database for due jobs (Claimed or RetryScheduled with `next_attempt <= now`)
- Load files from processing folder
- Upload files using `ApiClient`
- Update job state based on API response
- Handle retryable vs permanent failures using `RetryPolicy`
- Move files to sent or failed folders
- Manage bounded concurrency (e.g., 5 parallel uploads)

**Threading:** Single coordinator thread with bounded parallelism via `SemaphoreSlim`

---

#### 3. InboxEnumerator

**Purpose:** Scans inbox folder and applies eligibility filters.

**Filters:**
- File extension (`AllowedExtensions`)
- File size (`MaxFileSizeMB`)
- Path length (`MaxPathLength`)
- Zero-byte files (`SkipZeroByteFiles`)
- Hidden/system files (always skipped)
- Ignore patterns (e.g., files starting with `~`)

**Returns:** List of `FileInfo` objects for eligible files

---

#### 4. CompletenessChecker

**Purpose:** Determines if a file is complete and ready for processing.

**Implementation:**
```csharp
public bool IsComplete(string filePath, int stableAgeSeconds)
{
    // 1. Check stability age (last write time)
    var fileInfo = new FileInfo(filePath);
    var age = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
    if (age.TotalSeconds < stableAgeSeconds)
        return false;  // Still being modified

    // 2. Exclusive open test (FileShare.None)
    try
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        return true;  // Successfully opened exclusively
    }
    catch (IOException)
    {
        return false;  // Still locked by writer
    }
}
```

---

#### 5. FileClaimer

**Purpose:** Safely moves files from inbox to processing folder.

**Algorithm:**

```csharp
public string ClaimFile(string sourcePath, string jobId)
{
    var destFolder = Path.Combine(processingRoot, jobId);
    Directory.CreateDirectory(destFolder);

    var destPath = Path.Combine(destFolder, Path.GetFileName(sourcePath));

    try
    {
        // Attempt atomic move (same volume)
        File.Move(sourcePath, destPath);
        return destPath;
    }
    catch (IOException) when (IsOnDifferentVolume())
    {
        // Fallback for cross-volume: copy-verify-delete
        File.Copy(sourcePath, destPath);

        // Verify size
        var sourceSize = new FileInfo(sourcePath).Length;
        var destSize = new FileInfo(destPath).Length;
        if (sourceSize != destSize)
            throw new InvalidOperationException("Copy verification failed");

        // Delete source only after verification
        File.Delete(sourcePath);
        return destPath;
    }
}
```

---

#### 6. JobRepository

**Purpose:** Provides CRUD operations for job records in SQLite.

**Key Methods:**
- `CreateJobAsync(job)`: Insert new job record
- `GetDueJobsAsync(limit)`: Query jobs ready for sending
- `UpdateJobStateAsync(jobId, state)`: Update job status
- `RecordAttemptAsync(jobId, httpStatus, error)`: Log retry attempt
- `MarkSentAsync(jobId)`: Mark job as successfully delivered
- `MarkFailedAsync(jobId, reason)`: Move job to dead-letter

**Transactionality:** All state changes wrapped in SQLite transactions

---

#### 7. ApiClient

**Purpose:** Uploads files to remote API via HTTP.

**Implementation:**

```csharp
public async Task<ApiResponse> UploadFileAsync(string filePath, string fileName, string apiKey)
{
    using var content = new MultipartFormDataContent();
    using var fileStream = File.OpenRead(filePath);
    using var fileContent = new StreamContent(fileStream);

    content.Add(fileContent, "file", fileName);
    content.Add(new StringContent(fileName), "filename");

    var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
    request.Headers.Add(apiKeyHeaderName, apiKey);
    request.Content = content;

    var response = await httpClient.SendAsync(request, cancellationToken);

    return new ApiResponse
    {
        StatusCode = (int)response.StatusCode,
        IsSuccess = response.IsSuccessStatusCode,
        Body = await response.Content.ReadAsStringAsync()
    };
}
```

**Features:**
- Uses `IHttpClientFactory` for proper client lifecycle
- Adds API key header to all requests
- Streams file content (doesn't load entire file into memory)
- Respects configured timeouts

---

#### 8. RetryPolicy

**Purpose:** Calculates next retry time using exponential backoff with jitter.

**Algorithm:**

```csharp
public RetryDecision ShouldRetry(Job job, int httpStatus)
{
    // Check dead-letter conditions
    if (job.AttemptCount >= maxAttempts)
        return RetryDecision.DeadLetter("MaxAttemptsExceeded");

    var jobAge = DateTime.UtcNow - job.DiscoveredAtUtc;
    if (jobAge.TotalDays >= maxJobAgeDays)
        return RetryDecision.DeadLetter("MaxAgeExceeded");

    // Check if error is retryable
    if (!IsRetryableStatus(httpStatus))
        return RetryDecision.DeadLetter($"PermanentHttpError_{httpStatus}");

    // Calculate exponential backoff with jitter
    var delay = InitialDelaySeconds * Math.Pow(BackoffMultiplier, job.AttemptCount);
    delay = Math.Min(delay, MaxDelaySeconds);

    var jitter = Random.NextDouble() * JitterFactor * delay;
    var finalDelay = delay + (Random.NextDouble() > 0.5 ? jitter : -jitter);

    var nextAttempt = DateTime.UtcNow.AddSeconds(finalDelay);
    return RetryDecision.Retry(nextAttempt);
}

private bool IsRetryableStatus(int status)
{
    return status == 408 || status == 429 || status >= 500;
}
```

---

## State Machine

### Job State Machine Diagram

```
                ┌──────────────┐
                │  Discovered  │ (file found in inbox)
                └──────┬───────┘
                       │
                       │ Claim file
                       ▼
                ┌──────────────┐
                │   Claimed    │ (job created, file in processing)
                └──────┬───────┘
                       │
                       │ First send attempt
                       ▼
                ┌──────────────┐
                │   Sending    │
                └──────┬───────┘
                       │
           ┌───────────┴───────────┐
           │                       │
        Success                 Failure
           │                       │
           ▼                       ▼
    ┌─────────────┐     ┌──────────────────┐
    │    Sent     │     │  Check Retryable │
    └─────────────┘     └────────┬─────────┘
    (move to sent/)              │
                        ┌────────┴────────┐
                        │                 │
                    Retryable         Not Retryable
                        │                 │
                        ▼                 ▼
             ┌──────────────────┐   ┌────────────┐
             │  RetryScheduled  │   │   Failed   │
             └────────┬─────────┘   └────────────┘
                      │             (move to failed/)
                      │
                      │ Wait for next_attempt_at
                      │
                      └──────> (back to Sending)
```

### State Definitions

| State | Description | Next States |
|-------|-------------|-------------|
| `Discovered` | File found in inbox, not yet claimed | Claimed, (skipped) |
| `Claimed` | File moved to processing, job created | Sending |
| `Sending` | Upload attempt in progress | Sent, RetryScheduled, Failed |
| `RetryScheduled` | Failed with retry scheduled | Sending, Failed |
| `Sent` | Successfully delivered | (terminal) |
| `Failed` | Permanently failed | (terminal) |

---

## Database Schema

### Jobs Table

```sql
CREATE TABLE jobs (
    -- Identity
    job_id TEXT PRIMARY KEY,

    -- File information
    source_path TEXT NOT NULL,              -- Original inbox path
    processing_path TEXT,                   -- Current path in processing/
    file_size_bytes INTEGER,                -- File size
    file_mtime_utc TEXT,                    -- Original file modification time

    -- State tracking
    state TEXT NOT NULL,                    -- Current state (Claimed, Sending, etc.)
    discovered_at_utc TEXT NOT NULL,        -- When file was first discovered
    claimed_at_utc TEXT,                    -- When file was claimed
    sent_at_utc TEXT,                       -- When successfully delivered

    -- Retry tracking
    attempt_count INTEGER NOT NULL DEFAULT 0,
    next_attempt_at_utc TEXT,               -- When to retry next
    last_attempt TEXT,                      -- Timestamp of last attempt
    last_error TEXT,                        -- Last error message
    last_http_status INTEGER                -- Last HTTP status code
);

-- Indexes for efficient queries
CREATE INDEX idx_jobs_state_next_attempt
    ON jobs(state, next_attempt_at_utc);

CREATE UNIQUE INDEX idx_jobs_processing_path
    ON jobs(processing_path)
    WHERE processing_path IS NOT NULL;
```

### Example Queries

```sql
-- Get due jobs for sending
SELECT * FROM jobs
WHERE state IN ('Claimed', 'RetryScheduled')
  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= datetime('now'))
ORDER BY discovered_at_utc
LIMIT 10;

-- Get job statistics
SELECT state, COUNT(*) as count
FROM jobs
GROUP BY state;

-- Get failed jobs with errors
SELECT job_id, source_path, last_error, last_http_status, attempt_count
FROM jobs
WHERE state = 'Failed'
ORDER BY last_attempt DESC
LIMIT 20;
```

---

## Process Flows

### Scan Loop Flow

```
[Timer Triggers] (every ScanIntervalSeconds)
       │
       ▼
[Enumerate inbox files]
       │
       ▼
For each file:
       │
       ├──> [Check StableAge]
       │       │
       │       ├──> Too young? → Skip
       │       └──> Old enough ↓
       │
       ├──> [Check Eligibility]
       │       │
       │       ├──> Wrong extension/size? → Skip
       │       └──> Eligible ↓
       │
       ├──> [Completeness Check]
       │       │
       │       ├──> Locked? → Skip (retry next scan)
       │       └──> Complete ↓
       │
       ├──> [Check if already claimed]
       │       │
       │       ├──> Job exists? → Skip
       │       └──> New file ↓
       │
       ├──> [Claim file]
       │       │
       │       ├──> Move inbox → processing/<jobId>/
       │       └──> Success ↓
       │
       └──> [Create job in DB]
               │
               └──> State = Claimed

[Wait for next interval]
```

### Send Loop Flow

```
[Loop Continuously]
       │
       ▼
[Query DB for due jobs]
       │
       ├──> No jobs? → Sleep 5 seconds, loop
       └──> Jobs found ↓
       │
       ▼
For each job (bounded parallelism):
       │
       ├──> [Load file from processing/]
       │       │
       │       ├──> File missing? → Mark Failed
       │       └──> File found ↓
       │
       ├──> [Upload to API]
       │       │
       │       ├──> Success (2xx) ↓
       │       │       │
       │       │       ├──> [Move file to sent/]
       │       │       ├──> [Update DB: State=Sent]
       │       │       └──> Done
       │       │
       │       └──> Failure ↓
       │               │
       │               ├──> [Check RetryPolicy]
       │               │       │
       │               │       ├──> Should retry ↓
       │               │       │       │
       │               │       │       ├──> [Calculate backoff]
       │               │       │       ├──> [Update DB: State=RetryScheduled, next_attempt]
       │               │       │       └──> Done
       │               │       │
       │               │       └──> Dead-letter ↓
       │               │               │
       │               │               ├──> [Move file to failed/]
       │               │               ├──> [Update DB: State=Failed]
       │               │               └──> Done
       │               │
       │               └──> [Log error details]
       │
       └──> [Increment attempt_count]

[Loop continues]
```

---

## Concurrency and Threading

### Threading Model

```
┌─────────────────────────────────────────────────────────┐
│                   .NET Generic Host                     │
├─────────────────────────────────────────────────────────┤
│  Background Thread 1:                                   │
│  ┌──────────────────────────────────┐                   │
│  │  ScanLoopHostedService           │                   │
│  │  (ExecuteAsync loop)             │                   │
│  │  • Timer-based periodic scanning │                   │
│  │  • Single-threaded enumeration   │                   │
│  │  • Sequential file claiming      │                   │
│  └──────────────────────────────────┘                   │
│                                                          │
│  Background Thread 2:                                   │
│  ┌──────────────────────────────────┐                   │
│  │  SendLoopHostedService           │                   │
│  │  (ExecuteAsync loop)             │                   │
│  │  • Continuous loop with delays   │                   │
│  │  • Query DB for due jobs         │                   │
│  │  • SemaphoreSlim(5) for bounded  │                   │
│  │    parallelism (5 concurrent     │                   │
│  │    uploads via Task.WhenAll)     │                   │
│  └──────────────────────────────────┘                   │
│                                                          │
│  Serilog Background Threads:                            │
│  • Log file writer                                      │
│  • Event log writer                                     │
└─────────────────────────────────────────────────────────┘
```

### Concurrency Controls

**ScanLoop:**
- Single-threaded by design (one scan at a time)
- No parallelism in file enumeration/claiming
- Prevents duplicate processing

**SendLoop:**
- Bounded parallelism via `SemaphoreSlim(MaxConcurrentUploads)`
- Default: 5 concurrent uploads
- Each upload runs in a separate `Task`
- Coordinator thread waits for batch completion before next query

**Database:**
- SQLite with WAL mode enabled
- Multiple readers don't block each other
- Writers serialize automatically via SQLite locking

---

## Error Handling and Retry Strategy

### Error Categories

| Category | Examples | Handling |
|----------|----------|----------|
| **Transient** | Network timeouts, 503 Service Unavailable, 429 Rate Limit | Retry with exponential backoff |
| **Permanent** | 400 Bad Request, 401 Unauthorized, 404 Not Found | Dead-letter immediately |
| **Ambiguous** | Connection reset, TLS errors | Retry (conservative approach) |

### Exponential Backoff Example

With config: `InitialDelaySeconds=10`, `BackoffMultiplier=2.0`, `MaxDelaySeconds=3600`

| Attempt | Base Delay | With Jitter (±10%) | Cumulative Time |
|---------|------------|-------------------|-----------------|
| 1 | 10s | 9-11s | ~10s |
| 2 | 20s | 18-22s | ~30s |
| 3 | 40s | 36-44s | ~1m 10s |
| 4 | 80s | 72-88s | ~2m 30s |
| 5 | 160s | 144-176s | ~5m |
| 6 | 320s | 288-352s | ~10m |
| 7 | 640s | 576-704s | ~21m |
| 8 | 1280s | 1152-1408s | ~42m |
| 9 | 2560s | 2304-2816s | ~1h 24m |
| 10 | 3600s (capped) | 3240-3960s | ~2h 30m |

### Graceful Shutdown

On service stop signal:

1. Stop accepting new work (cancel scan/send loop tokens)
2. Wait for in-flight operations to complete (max 30 seconds)
3. Checkpoint current state to database
4. Flush logs
5. Exit gracefully

```csharp
public override async Task StopAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("Service stopping gracefully");

    // Cancel background loops
    _cancellationTokenSource.Cancel();

    // Wait for current operations (with timeout)
    var shutdownTimeout = Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    await Task.WhenAny(_currentWork, shutdownTimeout);

    _logger.LogInformation("Service stopped");
}
```

---

## Deployment Architecture

### Single-Instance Deployment

```
┌─────────────────────────────────────────────────────┐
│              Windows Server                         │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │     FolderToApiService.exe                  │   │
│  │     (Windows Service)                       │   │
│  │     Running as: NT AUTHORITY\SYSTEM         │   │
│  │     Auto-start: Yes                         │   │
│  │     Recovery: Restart on failure            │   │
│  └───────────┬─────────────────────────────────┘   │
│              │                                      │
│              ▼                                      │
│  ┌──────────────────────────────────┐              │
│  │  File System (Local or UNC)      │              │
│  │  C:\Data\FileDelivery\           │              │
│  │  └── inbox/                      │              │
│  │  └── processing/                 │              │
│  │  └── sent/                       │              │
│  │  └── failed/                     │              │
│  └──────────────────────────────────┘              │
│              │                                      │
│              ▼                                      │
│  ┌──────────────────────────────────┐              │
│  │  SQLite Database (Local)         │              │
│  │  C:\ProgramData\FolderToApi\     │              │
│  │  └── foldertoapi.db              │              │
│  └──────────────────────────────────┘              │
│              │                                      │
│              ▼                                      │
│  ┌──────────────────────────────────┐              │
│  │  Logs (Local)                    │              │
│  │  C:\Logs\FolderToApi\            │              │
│  │  └── log-YYYYMMDD.json           │              │
│  └──────────────────────────────────┘              │
└─────────────────────────────────────────────────────┘
              │
              │ HTTPS
              ▼
    ┌──────────────────┐
    │   Remote API     │
    │  (Internet)      │
    └──────────────────┘
```

### Multi-Instance Deployment (Future Enhancement)

```
┌──────────────────┐       ┌──────────────────┐
│   Server 1       │       │   Server 2       │
│  Service Instance│       │  Service Instance│
└────────┬─────────┘       └────────┬─────────┘
         │                          │
         └────────────┬─────────────┘
                      │
                      │ Shared DB Connection
                      ▼
            ┌──────────────────────┐
            │  Shared SQL Server/  │
            │  PostgreSQL          │
            │  (With Distributed   │
            │   Locking)           │
            └──────────────────────┘
                      │
                      │ Shared Files (UNC)
                      ▼
            ┌──────────────────────┐
            │  Network File Share  │
            │  \\fileserver\data   │
            └──────────────────────┘
```

---

## Design Decisions

### Why SQLite?

**Chosen:** SQLite
**Alternatives Considered:** SQL Server, PostgreSQL, filesystem-only

**Rationale:**
- ✅ Zero configuration (embedded)
- ✅ ACID transactions guarantee consistency
- ✅ WAL mode enables good concurrency
- ✅ Sufficient for single-instance deployment
- ✅ Simple backup (copy file)
- ❌ Not suitable for multi-instance HA (future: migrate to SQL Server)

### Why Periodic Scanning?

**Chosen:** Periodic scanning (polling)
**Alternative Considered:** FileSystemWatcher (event-driven)

**Rationale:**
- ✅ Deterministic and predictable
- ✅ Doesn't miss events (FileSystemWatcher buffer can overflow)
- ✅ Simpler error handling
- ✅ Throughput requirements don't need real-time response
- ❌ Higher latency (scan interval delay)

### Why Exclusive-Open Check?

**Chosen:** `FileShare.None` (exclusive open)
**Alternative Considered:** Size/timestamp stability heuristic only

**Rationale:**
- ✅ Definitive completeness check
- ✅ No false positives (file truly complete)
- ✅ Works reliably on local and SMB paths
- ❌ Adds small I/O overhead per file

### Why Store-and-Forward?

**Chosen:** Store-and-forward with durable queue
**Alternative Considered:** Direct pass-through (immediate upload on discovery)

**Rationale:**
- ✅ Guaranteed delivery semantics
- ✅ Survives API downtime
- ✅ Survives service restarts
- ✅ Enables retry with backoff
- ✅ Provides forensics and audit trail
- ❌ Requires disk space for processing/sent folders

---

## Next Steps

- **[API Integration Guide](api-integration.md)** - How the service interacts with APIs
- **[Security Guide](security.md)** - Security architecture and best practices
- **[Configuration Reference](configuration.md)** - Tune performance and behavior

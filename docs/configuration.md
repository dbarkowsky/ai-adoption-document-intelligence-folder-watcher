# Configuration Reference

Complete reference for all configuration options in the Folder-to-API Delivery Service.

## Table of Contents

1. [Configuration Files](#configuration-files)
2. [FolderWatcher](#folderwatcher)
3. [ApiClient](#apiclient)
4. [RetryPolicy](#retrypolicy)
5. [ArchiveRetention](#archiveretention)
6. [Database](#database)
7. [Logging](#logging)
8. [Environment-Specific Configuration](#environment-specific-configuration)
9. [Configuration Validation](#configuration-validation)
10. [Examples](#examples)

---

## Configuration Files

The service uses standard .NET configuration files with JSON format.

### File Locations

| File | Purpose | Location |
|------|---------|----------|
| `appsettings.json` | Base configuration | `C:\Program Files\FolderToApi\appsettings.json` |
| `appsettings.Production.json` | Production overrides | `C:\Program Files\FolderToApi\appsettings.Production.json` |
| `appsettings.Development.json` | Development overrides | `C:\Program Files\FolderToApi\appsettings.Development.json` |

### Configuration Hierarchy

Settings are loaded in order (later files override earlier ones):
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables (optional, advanced scenarios)

The environment is determined by the `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` variable (defaults to `Production`).

### Applying Configuration Changes

After modifying configuration files:

```powershell
# Restart the service to apply changes
Restart-Service FolderToApiService

# Verify service started successfully
Get-Service FolderToApiService
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

---

## FolderWatcher

Controls file scanning and filtering behavior.

### Schema

```json
{
  "FolderWatcher": {
    "RootPath": "C:\\Data\\FileDelivery",
    "ScanIntervalSeconds": 30,
    "StableAgeSeconds": 10,
    "AllowedExtensions": [".pdf", ".docx", ".doc", ".tif", ".tiff", ".jpg", ".jpeg", ".png"],
    "MaxFileSizeMB": 50,
    "RecursiveScan": false,
    "SkipZeroByteFiles": false,
    "MaxPathLength": 260
  }
}
```

### Properties

#### RootPath

**Type:** `string`
**Required:** Yes
**Default:** None

The root directory containing inbox, processing, sent, and failed subfolders.

**Supports:**
- Local NTFS paths: `C:\\Data\\FileDelivery`
- UNC paths: `\\\\fileserver\\share\\FileDelivery`
- Long paths (if enabled in Windows): `\\\\?\\C:\\Very\\Long\\Path`

**Important:**
- Use double backslashes in JSON: `"C:\\Data"`
- Service account must have Read/Write/Modify permissions
- Parent directory must exist; service creates subfolders automatically

**Example:**
```json
"RootPath": "C:\\Data\\FileDelivery"
```

---

#### ScanIntervalSeconds

**Type:** `integer`
**Required:** No
**Default:** `30`
**Valid Range:** `10` to `120`

How often (in seconds) the service scans the inbox folder for new files.

**Tuning Guidance:**
- **Low latency needed (10-15s)**: Use for time-sensitive deliveries
- **Standard throughput (30s)**: Balanced performance and CPU usage
- **High volume/low CPU (60-120s)**: Reduce scanning overhead for large folders

**Example:**
```json
"ScanIntervalSeconds": 30
```

---

#### StableAgeSeconds

**Type:** `integer`
**Required:** No
**Default:** `10`
**Valid Range:** `0` to `60`

Minimum age (seconds since last modification) before a file is considered stable and ready for processing.

**Purpose:** Prevents processing files that are still being written, especially over SMB shares where network delays can occur.

**Tuning Guidance:**
- **Local NTFS (5s)**: Files copied/written locally
- **SMB shares (10-15s)**: Network file copies
- **Slow networks (20-30s)**: Large files over slow connections

**Example:**
```json
"StableAgeSeconds": 10
```

---

#### AllowedExtensions

**Type:** `string[]`
**Required:** No
**Default:** `[".pdf", ".docx", ".doc", ".tif", ".tiff", ".jpg", ".jpeg", ".png"]`

List of file extensions to process. Only files with these extensions are eligible for delivery.

**Important:**
- Case-insensitive matching
- Must include the dot: `.pdf` not `pdf`
- Empty array means allow all extensions (not recommended)

**Example:**
```json
"AllowedExtensions": [".pdf", ".docx", ".jpg"]
```

---

#### MaxFileSizeMB

**Type:** `integer`
**Required:** No
**Default:** `50`
**Valid Range:** `1` to `2000`

Maximum file size in megabytes. Files exceeding this limit are skipped and logged as errors.

**Tuning Guidance:**
- Set based on API endpoint limits
- Consider network bandwidth and timeout settings
- Balance against disk space in processing/sent folders

**Example:**
```json
"MaxFileSizeMB": 50
```

---

#### RecursiveScan

**Type:** `boolean`
**Required:** No
**Default:** `false`

Whether to scan subdirectories under the inbox folder.

**Important:**
- `false`: Only scans files directly in `inbox\` (recommended)
- `true`: Scans all subdirectories recursively (may impact performance)

**Example:**
```json
"RecursiveScan": false
```

---

#### SkipZeroByteFiles

**Type:** `boolean`
**Required:** No
**Default:** `false`

Whether to skip zero-byte (empty) files during scanning.

**Options:**
- `false`: Process zero-byte files normally
- `true`: Skip zero-byte files and log warning

**Example:**
```json
"SkipZeroByteFiles": false
```

---

#### MaxPathLength

**Type:** `integer`
**Required:** No
**Default:** `260`
**Valid Range:** `100` to `32767`

Maximum allowed path length for file paths. Paths exceeding this limit are skipped.

**Note:** Windows traditionally has a 260-character limit unless long paths are enabled via registry or application manifest.

**Example:**
```json
"MaxPathLength": 260
```

---

## ApiClient

Controls HTTP API communication and authentication.

### Schema

```json
{
  "ApiClient": {
    "EndpointUrl": "https://api.example.com/upload",
    "ApiKeySource": "Configuration",
    "ApiKey": "your-api-key-here",
    "ApiKeyHeaderName": "X-API-Key",
    "ConnectTimeoutSeconds": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### Properties

#### EndpointUrl

**Type:** `string`
**Required:** Yes
**Default:** None

The HTTP(S) endpoint URL where files will be uploaded via POST request.

**Requirements:**
- Must be a valid HTTPS URL (HTTP not recommended for production)
- Must be reachable from the service host
- Should accept `multipart/form-data` POST requests

**Example:**
```json
"EndpointUrl": "https://api.example.com/v1/documents/upload"
```

---

#### ApiKeySource

**Type:** `string`
**Required:** No
**Default:** `"Configuration"`
**Valid Values:** `"Configuration"`, `"WindowsCredentialManager"`

Where the API key is stored.

**Options:**

1. **Configuration** (Simple, less secure)
   - API key stored directly in `appsettings.json`
   - Suitable for development/testing
   - Not recommended for production

2. **WindowsCredentialManager** (Recommended for production)
   - API key stored in Windows Credential Manager
   - Encrypted by Windows
   - Scoped to service account
   - See [Security Guide](security.md) for setup

**Example:**
```json
"ApiKeySource": "WindowsCredentialManager"
```

---

#### ApiKey

**Type:** `string`
**Required:** Conditional (only if `ApiKeySource = "Configuration"`)
**Default:** Empty string

The API key value (when using Configuration source).

**Security Warning:**
- Avoid storing API keys in configuration files for production
- Use WindowsCredentialManager instead
- Ensure proper file permissions if using Configuration source

**Example:**
```json
"ApiKey": "sk_live_abc123xyz789..."
```

---

#### ApiKeyHeaderName

**Type:** `string`
**Required:** No
**Default:** `"X-API-Key"`

The HTTP header name for API key authentication.

**Common Values:**
- `X-API-Key` (standard convention)
- `Authorization` (sometimes used, but typically for Bearer tokens)
- Custom headers as required by your API

**Example:**
```json
"ApiKeyHeaderName": "X-API-Key"
```

---

#### ConnectTimeoutSeconds

**Type:** `integer`
**Required:** No
**Default:** `10`
**Valid Range:** `1` to `60`

Maximum time (seconds) to establish a connection to the API endpoint.

**Tuning Guidance:**
- **Fast networks (5-10s)**: Standard setting
- **Slow networks (15-30s)**: Increase for high-latency connections
- **Timeout errors**: Increase if connection timeouts occur frequently

**Example:**
```json
"ConnectTimeoutSeconds": 10
```

---

#### RequestTimeoutSeconds

**Type:** `integer`
**Required:** No
**Default:** `60`
**Valid Range:** `10` to `600`

Maximum time (seconds) for the entire HTTP request (including upload time).

**Tuning Guidance:**
- Consider file size and upload bandwidth
- Formula: `(MaxFileSizeMB * 8) / (Upload Mbps) + 30s buffer`
- Example: 50 MB file at 10 Mbps = ~40s + 30s = 70s minimum

**Example:**
```json
"RequestTimeoutSeconds": 120
```

---

## RetryPolicy

Controls retry behavior for failed API deliveries.

### Schema

```json
{
  "RetryPolicy": {
    "MaxAttempts": 10,
    "MaxJobAgeDays": 7,
    "InitialDelaySeconds": 10,
    "MaxDelaySeconds": 3600,
    "BackoffMultiplier": 2.0,
    "JitterFactor": 0.1
  }
}
```

### Properties

#### MaxAttempts

**Type:** `integer`
**Required:** No
**Default:** `10`
**Valid Range:** `1` to `100`

Maximum number of delivery attempts before moving a file to dead-letter (failed folder).

**Tuning Guidance:**
- **Reliable API (5-10)**: Standard setting
- **Unreliable API (15-20)**: Increase for flaky endpoints
- **Quick failure (3-5)**: Reduce to fail fast

**Example:**
```json
"MaxAttempts": 10
```

---

#### MaxJobAgeDays

**Type:** `integer`
**Required:** No
**Default:** `7`
**Valid Range:** `1` to `90`

Maximum age (days since discovery) before moving a file to dead-letter, regardless of attempt count.

**Purpose:** Prevents jobs from retrying indefinitely if API is down for extended periods.

**Example:**
```json
"MaxJobAgeDays": 7
```

---

#### InitialDelaySeconds

**Type:** `integer`
**Required:** No
**Default:** `10`
**Valid Range:** `1` to `3600`

Initial retry delay in seconds after the first failure.

**Backoff Schedule:** Subsequent delays are calculated as:
```
next_delay = min(InitialDelay * (BackoffMultiplier ^ attempt), MaxDelay) ± jitter
```

**Example:**
```json
"InitialDelaySeconds": 10
```

---

#### MaxDelaySeconds

**Type:** `integer`
**Required:** No
**Default:** `3600` (1 hour)
**Valid Range:** `60` to `86400`

Maximum retry delay in seconds (cap for exponential backoff).

**Tuning Guidance:**
- **Fast recovery (600s / 10 min)**: For transient failures
- **Standard (3600s / 1 hour)**: Balanced approach
- **Extended outages (7200s / 2 hours)**: For planned maintenance windows

**Example:**
```json
"MaxDelaySeconds": 3600
```

---

#### BackoffMultiplier

**Type:** `decimal`
**Required:** No
**Default:** `2.0`
**Valid Range:** `1.0` to `10.0`

The exponential backoff multiplier applied to each retry delay.

**Common Values:**
- `1.0`: Linear backoff (constant delay)
- `2.0`: Standard exponential backoff (doubles each time)
- `1.5`: Gentler exponential backoff

**Example Backoff Sequences (InitialDelay=10s, MaxDelay=3600s):**
- Multiplier 2.0: 10s, 20s, 40s, 80s, 160s, 320s, 640s, 1280s, 2560s, 3600s (capped)
- Multiplier 1.5: 10s, 15s, 22.5s, 33.75s, 50.6s, 75.9s, 113.9s, 170.8s, 256.2s, ...

**Example:**
```json
"BackoffMultiplier": 2.0
```

---

#### JitterFactor

**Type:** `decimal`
**Required:** No
**Default:** `0.1`
**Valid Range:** `0.0` to `0.5`

Random jitter applied to retry delays to prevent thundering herd.

**Formula:** `actual_delay = calculated_delay * (1 ± JitterFactor * random[0,1])`

**Example:** With JitterFactor=0.1 and calculated delay of 100s:
- Actual delay will be between 90s and 110s (±10%)

**Example:**
```json
"JitterFactor": 0.1
```

---

## ArchiveRetention

Controls automatic cleanup of processed files.

### Schema

```json
{
  "ArchiveRetention": {
    "SentRetentionDays": 30,
    "FailedRetentionDays": 90,
    "EnableAutoCleanup": true,
    "CleanupIntervalHours": 24
  }
}
```

### Properties

#### SentRetentionDays

**Type:** `integer`
**Required:** No
**Default:** `30`
**Valid Range:** `1` to `3650`

Number of days to retain successfully delivered files in the `sent\` folder before automatic deletion.

**Tuning Guidance:**
- **Short retention (7-14 days)**: Limited disk space
- **Standard retention (30 days)**: Balance between audit trail and storage
- **Long retention (90-365 days)**: Compliance or audit requirements

**Example:**
```json
"SentRetentionDays": 30
```

---

#### FailedRetentionDays

**Type:** `integer`
**Required:** No
**Default:** `90`
**Valid Range:** `1` to `3650`

Number of days to retain failed files in the `failed\` folder before automatic deletion.

**Note:** Failed files typically require manual review, so longer retention is recommended.

**Example:**
```json
"FailedRetentionDays": 90
```

---

#### EnableAutoCleanup

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether automatic cleanup of archived files is enabled.

**Options:**
- `true`: Automatically delete old files based on retention settings
- `false`: Manual cleanup required (files accumulate indefinitely)

**Example:**
```json
"EnableAutoCleanup": true
```

---

#### CleanupIntervalHours

**Type:** `integer`
**Required:** No
**Default:** `24`
**Valid Range:** `1` to `168`

How often (in hours) the cleanup process runs.

**Tuning Guidance:**
- **Daily (24 hours)**: Standard setting
- **More frequent (6-12 hours)**: High-volume scenarios with limited disk space
- **Less frequent (48-72 hours)**: Low-volume scenarios

**Example:**
```json
"CleanupIntervalHours": 24
```

---

## Database

Controls SQLite database configuration.

### Schema

```json
{
  "Database": {
    "ConnectionString": "Data Source=C:\\ProgramData\\FolderToApi\\foldertoapi.db",
    "EnableWalMode": true
  }
}
```

### Properties

#### ConnectionString

**Type:** `string`
**Required:** No
**Default:** `"Data Source=foldertoapi.db"` (relative to service executable)

SQLite connection string specifying database file location.

**Important:**
- Store on local disk (not network share) for best performance
- Use absolute paths for production: `Data Source=C:\\ProgramData\\FolderToApi\\foldertoapi.db`
- Service account needs Read/Write permissions to the database file and directory
- Double backslashes in JSON: `C:\\Path\\To\\Database`

**Example:**
```json
"ConnectionString": "Data Source=C:\\ProgramData\\FolderToApi\\foldertoapi.db"
```

---

#### EnableWalMode

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether to enable SQLite Write-Ahead Logging (WAL) mode.

**Benefits of WAL Mode:**
- Better concurrency (readers don't block writers)
- Improved performance for write-heavy workloads
- Atomic commits

**Recommended:** Leave enabled unless you have specific reasons to disable.

**Example:**
```json
"EnableWalMode": true
```

---

## Logging

Controls logging behavior, destinations, and rotation.

### Schema

```json
{
  "Logging": {
    "LogDirectory": "C:\\Logs\\FolderToApi",
    "MinimumLevel": "Information",
    "WriteToConsole": true,
    "WriteToFile": true,
    "WriteToEventLog": true,
    "RollingInterval": "Day",
    "FileSizeLimitBytes": 104857600,
    "RetainedFileCountLimit": 30,
    "UseJsonFormatting": true
  }
}
```

### Properties

#### LogDirectory

**Type:** `string`
**Required:** No
**Default:** `"C:\\Logs\\FolderToApi"`

Directory where log files are written.

**Important:**
- Service account needs Write permissions
- Directory is created automatically if it doesn't exist
- Use local disk for best performance

**Example:**
```json
"LogDirectory": "C:\\Logs\\FolderToApi"
```

---

#### MinimumLevel

**Type:** `string`
**Required:** No
**Default:** `"Information"`
**Valid Values:** `"Verbose"`, `"Debug"`, `"Information"`, `"Warning"`, `"Error"`, `"Fatal"`

Minimum log level to capture.

**Level Guide:**
- **Verbose/Debug**: Detailed diagnostic information (high volume)
- **Information**: Normal operational messages (recommended for production)
- **Warning**: Potential issues that don't stop execution
- **Error**: Errors that may impact functionality
- **Fatal**: Critical errors that stop the service

**Example:**
```json
"MinimumLevel": "Information"
```

---

#### WriteToConsole

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether to write logs to console (visible when running interactively, captured by Windows Service host).

**Example:**
```json
"WriteToConsole": true
```

---

#### WriteToFile

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether to write logs to JSON files in the log directory.

**Example:**
```json
"WriteToFile": true
```

---

#### WriteToEventLog

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether to write logs to Windows Event Log (Application log, source: FolderToApiService).

**Example:**
```json
"WriteToEventLog": true
```

---

#### RollingInterval

**Type:** `string`
**Required:** No
**Default:** `"Day"`
**Valid Values:** `"Infinite"`, `"Year"`, `"Month"`, `"Day"`, `"Hour"`, `"Minute"`

How often log files roll over to a new file.

**Common Values:**
- **Day**: New log file each day (recommended)
- **Hour**: High-volume logging scenarios
- **Infinite**: Single log file (not recommended)

**Example:**
```json
"RollingInterval": "Day"
```

---

#### FileSizeLimitBytes

**Type:** `integer`
**Required:** No
**Default:** `104857600` (100 MB)
**Valid Range:** `1048576` (1 MB) to `1073741824` (1 GB)

Maximum size of a single log file before rolling to a new file.

**Example:**
```json
"FileSizeLimitBytes": 104857600
```

---

#### RetainedFileCountLimit

**Type:** `integer`
**Required:** No
**Default:** `30`
**Valid Range:** `1` to `365`

Number of log files to retain before automatically deleting oldest files.

**Example:**
```json
"RetainedFileCountLimit": 30
```

---

#### UseJsonFormatting

**Type:** `boolean`
**Required:** No
**Default:** `true`

Whether to use structured JSON formatting for log files.

**Options:**
- `true`: Machine-readable JSON (recommended for log analysis tools)
- `false`: Human-readable plain text

**Example:**
```json
"UseJsonFormatting": true
```

---

## Environment-Specific Configuration

Use environment-specific files to override base configuration.

### Example: Production Configuration

**File:** `appsettings.Production.json`

```json
{
  "FolderWatcher": {
    "RootPath": "\\\\fileserver\\production\\FileDelivery"
  },
  "ApiClient": {
    "EndpointUrl": "https://api.production.example.com/upload",
    "ApiKeySource": "WindowsCredentialManager"
  },
  "Logging": {
    "MinimumLevel": "Information",
    "WriteToConsole": false
  }
}
```

### Example: Development Configuration

**File:** `appsettings.Development.json`

```json
{
  "FolderWatcher": {
    "RootPath": "C:\\Dev\\TestFiles",
    "ScanIntervalSeconds": 10
  },
  "ApiClient": {
    "EndpointUrl": "https://api.dev.example.com/upload"
  },
  "Logging": {
    "MinimumLevel": "Debug",
    "WriteToEventLog": false
  }
}
```

---

## Configuration Validation

The service validates configuration on startup and logs errors to the Windows Event Log.

### Common Validation Errors

| Error | Cause | Solution |
|-------|-------|----------|
| RootPath is required | Missing or empty RootPath | Set valid path in configuration |
| EndpointUrl is required | Missing or empty EndpointUrl | Set valid HTTPS URL |
| Invalid ScanIntervalSeconds | Value outside valid range | Use value between 10 and 120 |
| Invalid MaxFileSizeMB | Value outside valid range | Use value between 1 and 2000 |
| Database connection failed | Invalid connection string or permissions | Verify path and permissions |

### Testing Configuration

Before restarting the service, validate configuration syntax:

```powershell
# Test JSON syntax
Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json

# Test service restart with validation
Restart-Service FolderToApiService
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

---

## Examples

### Example 1: Standard Production Configuration

```json
{
  "FolderWatcher": {
    "RootPath": "C:\\Data\\FileDelivery",
    "ScanIntervalSeconds": 30,
    "StableAgeSeconds": 10,
    "AllowedExtensions": [".pdf", ".docx"],
    "MaxFileSizeMB": 50
  },
  "ApiClient": {
    "EndpointUrl": "https://api.example.com/upload",
    "ApiKeySource": "WindowsCredentialManager",
    "ApiKeyHeaderName": "X-API-Key",
    "RequestTimeoutSeconds": 120
  },
  "RetryPolicy": {
    "MaxAttempts": 10,
    "MaxJobAgeDays": 7,
    "InitialDelaySeconds": 10,
    "MaxDelaySeconds": 3600
  },
  "ArchiveRetention": {
    "SentRetentionDays": 30,
    "FailedRetentionDays": 90,
    "EnableAutoCleanup": true
  },
  "Database": {
    "ConnectionString": "Data Source=C:\\ProgramData\\FolderToApi\\foldertoapi.db"
  },
  "Logging": {
    "LogDirectory": "C:\\Logs\\FolderToApi",
    "MinimumLevel": "Information",
    "WriteToFile": true,
    "WriteToEventLog": true
  }
}
```

### Example 2: High-Volume Configuration

```json
{
  "FolderWatcher": {
    "RootPath": "D:\\Intake\\HighVolume",
    "ScanIntervalSeconds": 15,
    "StableAgeSeconds": 5,
    "MaxFileSizeMB": 25
  },
  "ApiClient": {
    "RequestTimeoutSeconds": 90,
    "ConnectTimeoutSeconds": 15
  },
  "ArchiveRetention": {
    "SentRetentionDays": 14,
    "CleanupIntervalHours": 12
  }
}
```

### Example 3: UNC Path with Extended Retry

```json
{
  "FolderWatcher": {
    "RootPath": "\\\\fileserver\\share\\FileDelivery",
    "ScanIntervalSeconds": 60,
    "StableAgeSeconds": 15
  },
  "RetryPolicy": {
    "MaxAttempts": 20,
    "MaxJobAgeDays": 14,
    "MaxDelaySeconds": 7200
  }
}
```

---

## Next Steps

- **[Operations Guide](operations.md)** - Learn how to manage and monitor the service
- **[Security Guide](security.md)** - Secure API key storage and permissions
- **[Troubleshooting Guide](troubleshooting.md)** - Resolve configuration issues

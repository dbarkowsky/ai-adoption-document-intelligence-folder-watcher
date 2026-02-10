# Troubleshooting Guide

Comprehensive troubleshooting guide for the Folder-to-API Delivery Service.

## Table of Contents

1. [Quick Diagnostic Steps](#quick-diagnostic-steps)
2. [Service Won't Start](#service-wont-start)
3. [Files Not Being Processed](#files-not-being-processed)
4. [API Delivery Failures](#api-delivery-failures)
5. [Performance Issues](#performance-issues)
6. [Database Issues](#database-issues)
7. [Network and Connectivity](#network-and-connectivity)
8. [Permission Problems](#permission-problems)
9. [Log Analysis](#log-analysis)
10. [Getting Additional Help](#getting-additional-help)

---

## Quick Diagnostic Steps

When encountering issues, follow these quick diagnostic steps:

```powershell
# 1. Check service status
Get-Service FolderToApiService

# 2. Check recent Event Log entries
Get-EventLog -LogName Application -Source FolderToApiService -Newest 10

# 3. Check folder status
$root = "C:\Data\FileDelivery"
Get-ChildItem "$root\inbox", "$root\processing", "$root\failed" -Recurse | Measure-Object

# 4. Check process health
Get-Process -Name "FolderToApi.Service" -ErrorAction SilentlyContinue | Format-Table Name, CPU, WorkingSet, StartTime

# 5. Review recent logs
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Tail 20
```

---

## Service Won't Start

### Symptom

Service status shows "Stopped" immediately after attempting to start, or service start command fails.

### Diagnostic Steps

#### 1. Check Event Log for Errors

```powershell
Get-EventLog -LogName Application -Source FolderToApiService -EntryType Error -Newest 5
```

#### 2. Validate Configuration File

```powershell
# Test JSON syntax
try {
    $config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
    Write-Host "Configuration file is valid JSON" -ForegroundColor Green

    # Check required settings
    if ([string]::IsNullOrWhiteSpace($config.FolderWatcher.RootPath)) {
        Write-Host "ERROR: RootPath is not configured" -ForegroundColor Red
    }
    if ([string]::IsNullOrWhiteSpace($config.ApiClient.EndpointUrl)) {
        Write-Host "ERROR: EndpointUrl is not configured" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: Configuration file has invalid JSON syntax" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
```

### Common Causes and Solutions

| Error Message | Cause | Solution |
|---------------|-------|----------|
| "RootPath is required" | Missing or empty RootPath in config | Set valid path in `appsettings.json`: `"RootPath": "C:\\Data\\FileDelivery"` |
| "EndpointUrl is required" | Missing or empty EndpointUrl | Set valid HTTPS URL in `appsettings.json` |
| "Access to path denied" | Service account lacks permissions | Grant Read/Write permissions to service account (see [Permission Problems](#permission-problems)) |
| "Database connection failed" | Database file locked or inaccessible | Check database file permissions and ensure no other process has it open |
| "Could not load file or assembly" | Missing runtime dependencies | Ensure .NET 10.0 Runtime is installed or use self-contained deployment |
| "Invalid configuration" | JSON syntax error | Validate JSON using PowerShell or online JSON validator |

### Step-by-Step Resolution

#### Configuration Error

```powershell
# 1. Backup current config
Copy-Item "C:\Program Files\FolderToApi\appsettings.json" "C:\Program Files\FolderToApi\appsettings.json.backup"

# 2. Edit configuration
notepad "C:\Program Files\FolderToApi\appsettings.json"

# 3. Ensure minimum required settings:
# {
#   "FolderWatcher": {
#     "RootPath": "C:\\Data\\FileDelivery"
#   },
#   "ApiClient": {
#     "EndpointUrl": "https://api.example.com/upload",
#     "ApiKey": "your-api-key"
#   }
# }

# 4. Validate and restart
try {
    Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json | Out-Null
    Restart-Service FolderToApiService
    Write-Host "Service restarted successfully" -ForegroundColor Green
} catch {
    Write-Host "Configuration still invalid, restoring backup" -ForegroundColor Red
    Copy-Item "C:\Program Files\FolderToApi\appsettings.json.backup" "C:\Program Files\FolderToApi\appsettings.json" -Force
}
```

#### Permission Error

```powershell
# Check current service account
sc qc FolderToApiService

# Grant permissions to service account (replace DOMAIN\ServiceAccount)
$serviceAccount = "NT AUTHORITY\SYSTEM"  # Or your custom account
$rootPath = "C:\Data\FileDelivery"
$dbPath = "C:\ProgramData\FolderToApi"
$logPath = "C:\Logs\FolderToApi"

# Grant permissions
icacls $rootPath /grant "${serviceAccount}:(OI)(CI)M" /T
icacls $dbPath /grant "${serviceAccount}:(OI)(CI)M" /T
icacls $logPath /grant "${serviceAccount}:(OI)(CI)M" /T

# Restart service
Restart-Service FolderToApiService
```

#### Database Locked

```powershell
# Check if database is locked by another process
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"

# Find processes with handles to the database
$handles = handle.exe -a -u $dbPath 2>$null  # Requires Sysinternals handle.exe
if ($handles) {
    Write-Host "Database is locked by:" -ForegroundColor Yellow
    $handles
} else {
    Write-Host "No locks found on database" -ForegroundColor Green
}

# If locked by old service instance, force stop
Stop-Service FolderToApiService -Force
Start-Sleep -Seconds 2
Start-Service FolderToApiService
```

---

## Files Not Being Processed

### Symptom

Files remain in inbox folder and are not being moved to processing or delivered.

### Diagnostic Steps

#### 1. Verify Service is Running

```powershell
$service = Get-Service FolderToApiService
if ($service.Status -ne 'Running') {
    Write-Host "Service is not running!" -ForegroundColor Red
    Start-Service FolderToApiService
} else {
    Write-Host "Service is running" -ForegroundColor Green
}
```

#### 2. Check for File Locks

```powershell
$inboxPath = "C:\Data\FileDelivery\inbox"
Get-ChildItem $inboxPath | ForEach-Object {
    try {
        $file = [IO.File]::Open($_.FullName, 'Open', 'Read', 'None')
        $file.Close()
        Write-Host "$($_.Name) - Ready for processing" -ForegroundColor Green
    } catch {
        Write-Host "$($_.Name) - LOCKED (still being written)" -ForegroundColor Yellow
    }
}
```

#### 3. Check File Eligibility

```powershell
# Check if files meet criteria (extension, size)
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
$allowedExt = $config.FolderWatcher.AllowedExtensions
$maxSizeMB = $config.FolderWatcher.MaxFileSizeMB

Get-ChildItem $inboxPath | ForEach-Object {
    $ext = $_.Extension.ToLower()
    $sizeMB = [math]::Round($_.Length / 1MB, 2)

    $eligible = $allowedExt -contains $ext -and $sizeMB -le $maxSizeMB
    $reason = if (-not ($allowedExt -contains $ext)) { "Extension not allowed" }
              elseif ($sizeMB -gt $maxSizeMB) { "File too large ($sizeMB MB > $maxSizeMB MB)" }
              else { "Eligible" }

    $color = if ($eligible) { 'Green' } else { 'Red' }
    Write-Host "$($_.Name) - $reason" -ForegroundColor $color
}
```

#### 4. Review Scan Loop Logs

```powershell
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern "ScanLoopHostedService" | Select-Object -Last 20
```

### Common Causes and Solutions

| Cause | Detection | Solution |
|-------|-----------|----------|
| Service not running | `Get-Service FolderToApiService` shows Stopped | Start service: `Start-Service FolderToApiService` |
| Files still being written | File locked (cannot open with exclusive access) | Wait for file write to complete; adjust `StableAgeSeconds` |
| Wrong file extension | File extension not in `AllowedExtensions` | Add extension to config or rename file |
| File too large | File size exceeds `MaxFileSizeMB` | Increase limit or split file |
| Incorrect root path | Service scanning wrong folder | Verify `RootPath` in configuration |
| StableAge not met | File modified recently | Wait for `StableAgeSeconds` to pass |
| Scan loop crashed | No scan activity in logs | Restart service |

### Step-by-Step Resolution

#### Files Locked

Files may remain locked if still being copied (especially over slow networks):

```powershell
# Check file modification times
Get-ChildItem "C:\Data\FileDelivery\inbox" | Select-Object Name, LastWriteTime, @{Label="Age (seconds)";Expression={(New-TimeSpan -Start $_.LastWriteTime).TotalSeconds}}

# If files are old but still locked, identify locking process
# Requires Sysinternals handle.exe or Process Explorer
```

**Solution:** Increase `StableAgeSeconds` in configuration:

```json
{
  "FolderWatcher": {
    "StableAgeSeconds": 20
  }
}
```

#### Wrong File Extension

```powershell
# Check current allowed extensions
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
Write-Host "Allowed extensions: $($config.FolderWatcher.AllowedExtensions -join ', ')"

# Add new extension if needed
$config.FolderWatcher.AllowedExtensions += ".xlsx"
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

---

## API Delivery Failures

### Symptom

Files move from inbox to processing but then to failed folder instead of sent.

### Diagnostic Steps

#### 1. Check Failed Folder

```powershell
Get-ChildItem "C:\Data\FileDelivery\failed" | Format-Table Name, Length, LastWriteTime
```

#### 2. Query Database for Error Details

```powershell
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT job_id, source_path, last_error, last_http_status, attempt_count, last_attempt FROM jobs WHERE state = 'Failed' ORDER BY last_attempt DESC LIMIT 10;"
```

#### 3. Search Logs for HTTP Errors

```powershell
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern '"Level":"Error".*HTTP' | Select-Object -Last 20
```

### Common HTTP Status Codes

| Status Code | Meaning | Likely Cause | Solution |
|-------------|---------|--------------|----------|
| 400 | Bad Request | Invalid request format | Check API documentation for required format |
| 401 | Unauthorized | Invalid or missing API key | Verify API key is correct |
| 403 | Forbidden | Valid key but insufficient permissions | Check API key has upload permissions |
| 404 | Not Found | Endpoint URL incorrect | Verify `EndpointUrl` in configuration |
| 413 | Payload Too Large | File exceeds API size limit | Reduce `MaxFileSizeMB` or contact API provider |
| 415 | Unsupported Media Type | API doesn't accept file type | Check API documentation for supported types |
| 429 | Too Many Requests | Rate limit exceeded | Reduce throughput or contact API provider |
| 500 | Internal Server Error | API server error | Retry (automatic), contact API provider if persistent |
| 502/503 | Bad Gateway/Service Unavailable | API temporarily down | Wait for API recovery (automatic retries) |
| 504 | Gateway Timeout | API response too slow | Increase `RequestTimeoutSeconds` or check API performance |

### Common Causes and Solutions

#### Invalid or Expired API Key (401/403)

```powershell
# Test API key manually
$apiKey = "your-api-key-here"
$endpoint = "https://api.example.com/upload"

try {
    $response = Invoke-WebRequest -Uri $endpoint -Method POST -Headers @{"X-API-Key" = $apiKey} -UseBasicParsing
    Write-Host "API key is valid (HTTP $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "API key test failed: $($_.Exception.Message)" -ForegroundColor Red
}
```

**Solution:** Update API key in configuration or Credential Manager (see [Operations Guide](operations.md#rotate-api-key))

#### Incorrect Endpoint URL (404)

```powershell
# Verify endpoint URL
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
Write-Host "Current endpoint: $($config.ApiClient.EndpointUrl)"

# Test endpoint connectivity
Test-NetConnection -ComputerName ([System.Uri]$config.ApiClient.EndpointUrl).Host -Port 443
```

**Solution:** Correct the `EndpointUrl` in configuration

#### API Timeout (504)

```powershell
# Check current timeout settings
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
Write-Host "Connect timeout: $($config.ApiClient.ConnectTimeoutSeconds)s"
Write-Host "Request timeout: $($config.ApiClient.RequestTimeoutSeconds)s"

# Increase timeouts for large files or slow networks
$config.ApiClient.RequestTimeoutSeconds = 180
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

---

## Performance Issues

### Symptom

High CPU usage, slow processing, or growing backlog.

### Diagnostic Steps

#### 1. Check Resource Usage

```powershell
# Monitor CPU and memory
Get-Process -Name "FolderToApi.Service" | Format-Table Name, CPU, @{Label="Memory (MB)";Expression={[math]::Round($_.WorkingSet64/1MB,2)}}, Threads, StartTime
```

#### 2. Check Backlog Size

```powershell
$root = "C:\Data\FileDelivery"
Write-Host "Inbox: $((Get-ChildItem "$root\inbox").Count) files"
Write-Host "Processing: $((Get-ChildItem "$root\processing" -Recurse -File).Count) files"
Write-Host "Sent: $((Get-ChildItem "$root\sent").Count) files"
Write-Host "Failed: $((Get-ChildItem "$root\failed").Count) files"
```

#### 3. Monitor Database Size

```powershell
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"
$dbSize = [math]::Round((Get-Item $dbPath).Length / 1MB, 2)
Write-Host "Database size: $dbSize MB"

# Check job counts
sqlite3 $dbPath "SELECT state, COUNT(*) FROM jobs GROUP BY state;"
```

### Common Causes and Solutions

#### High CPU Usage During Scanning

**Cause:** ScanIntervalSeconds is too low or RecursiveScan is enabled on a large folder structure.

**Solution:**

```powershell
# Increase scan interval
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
$config.FolderWatcher.ScanIntervalSeconds = 60  # Increase from 30 to 60
$config.FolderWatcher.RecursiveScan = $false    # Disable if not needed
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

#### Memory Growth

**Cause:** Large files or many concurrent operations.

**Solution:**
- Reduce `MaxFileSizeMB` to prevent loading large files into memory
- Monitor for memory leaks in logs
- Restart service if memory usage is excessive:

```powershell
# Restart service during maintenance window
Restart-Service FolderToApiService
```

#### Slow API Responses

**Cause:** API endpoint experiencing high latency.

**Solution:**
- Check network latency: `Test-NetConnection api.example.com -Port 443`
- Review API provider status page
- Increase timeouts if necessary
- Contact API provider for performance issues

#### Database Growth

**Cause:** Old job records not being cleaned up.

**Solution:**

```powershell
# Manually purge old completed jobs (older than 90 days)
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"
sqlite3 $dbPath "DELETE FROM jobs WHERE state IN ('Sent') AND sent_at_utc < datetime('now', '-90 days');"

# Vacuum database to reclaim space
sqlite3 $dbPath "VACUUM;"

# Check new size
$newSize = [math]::Round((Get-Item $dbPath).Length / 1MB, 2)
Write-Host "Database size after cleanup: $newSize MB" -ForegroundColor Green
```

---

## Database Issues

### Symptom

Database errors, corruption, or "database is locked" messages.

### Diagnostic Steps

#### 1. Check Database File

```powershell
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"
if (Test-Path $dbPath) {
    $dbInfo = Get-Item $dbPath
    Write-Host "Database exists: $($dbInfo.FullName)" -ForegroundColor Green
    Write-Host "Size: $([math]::Round($dbInfo.Length / 1MB, 2)) MB"
    Write-Host "Last modified: $($dbInfo.LastWriteTime)"
} else {
    Write-Host "Database file not found!" -ForegroundColor Red
}
```

#### 2. Test Database Connectivity

```powershell
# Test with SQLite CLI
sqlite3 $dbPath "SELECT COUNT(*) as job_count FROM jobs;"
```

### Common Database Issues

#### Database Locked

**Cause:** Another process has the database open, or service didn't shut down cleanly.

**Solution:**

```powershell
# Stop service
Stop-Service FolderToApiService -Force

# Wait for file handles to release
Start-Sleep -Seconds 5

# Check for SQLite temp files
Get-Item "C:\ProgramData\FolderToApi\foldertoapi.db-shm", "C:\ProgramData\FolderToApi\foldertoapi.db-wal" -ErrorAction SilentlyContinue

# Restart service
Start-Service FolderToApiService
```

#### Database Corruption

**Symptoms:** "database disk image is malformed" or similar errors.

**Recovery Steps:**

```powershell
# Stop service
Stop-Service FolderToApiService

# Backup corrupted database
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"
Copy-Item $dbPath "$dbPath.corrupted-$(Get-Date -Format 'yyyyMMddHHmmss')"

# Attempt SQLite recovery
sqlite3 $dbPath "PRAGMA integrity_check;"

# If recovery fails, restore from backup
$latestBackup = Get-ChildItem "C:\Backups\FolderToApi\*\foldertoapi.db" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latestBackup) {
    Copy-Item $latestBackup.FullName $dbPath -Force
    Write-Host "Database restored from backup: $($latestBackup.FullName)" -ForegroundColor Green
} else {
    Write-Host "No backup found! Database will be recreated (job history lost)" -ForegroundColor Yellow
    Remove-Item $dbPath -Force
}

# Restart service (will recreate database if needed)
Start-Service FolderToApiService
```

---

## Network and Connectivity

### Symptom

Cannot reach API endpoint, network errors, or TLS/SSL failures.

### Diagnostic Steps

#### 1. Test Network Connectivity

```powershell
$endpoint = "https://api.example.com"
$host = ([System.Uri]$endpoint).Host

# Test basic connectivity
Test-NetConnection -ComputerName $host -Port 443

# Test DNS resolution
Resolve-DnsName $host

# Test HTTPS endpoint
try {
    $response = Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 10
    Write-Host "Endpoint is reachable (HTTP $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "Endpoint not reachable: $($_.Exception.Message)" -ForegroundColor Red
}
```

#### 2. Check Firewall Rules

```powershell
# Check if service executable is allowed through firewall
Get-NetFirewallApplicationFilter | Where-Object { $_.Program -like "*FolderToApi*" }

# Check outbound HTTPS rule
Get-NetFirewallRule | Where-Object { $_.Direction -eq 'Outbound' -and $_.Enabled -eq $true } | Select-Object DisplayName, Action
```

#### 3. Check Proxy Settings

```powershell
# Check system proxy settings
Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' | Select-Object ProxyEnable, ProxyServer
```

### Common Causes and Solutions

#### Network Timeout

**Cause:** Slow network, high latency, or API endpoint unresponsive.

**Solution:**
- Increase `ConnectTimeoutSeconds` and `RequestTimeoutSeconds`
- Check network path to API endpoint
- Verify no network routing issues

#### TLS/SSL Certificate Errors

**Cause:** Expired certificate, self-signed certificate, or TLS version mismatch.

**Solution:**

```powershell
# Test TLS connection
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
try {
    $response = Invoke-WebRequest -Uri "https://api.example.com" -UseBasicParsing
    Write-Host "TLS connection successful" -ForegroundColor Green
} catch {
    Write-Host "TLS error: $($_.Exception.Message)" -ForegroundColor Red
}

# Ensure TLS 1.2 is enabled on server
# See https://docs.microsoft.com/en-us/windows-server/security/tls/tls-registry-settings
```

#### Firewall Blocking

**Solution:**

```powershell
# Allow outbound HTTPS for service
New-NetFirewallRule -DisplayName "FolderToApiService Outbound HTTPS" `
    -Direction Outbound `
    -Program "C:\Program Files\FolderToApi\FolderToApi.Service.exe" `
    -Protocol TCP `
    -RemotePort 443 `
    -Action Allow
```

---

## Permission Problems

### Symptom

"Access denied" errors for folders, files, or database.

### Diagnostic Steps

#### 1. Check Service Account

```powershell
# Get service account
$service = Get-WmiObject Win32_Service -Filter "Name='FolderToApiService'"
Write-Host "Service running as: $($service.StartName)"
```

#### 2. Check Folder Permissions

```powershell
$paths = @(
    "C:\Data\FileDelivery",
    "C:\ProgramData\FolderToApi",
    "C:\Logs\FolderToApi"
)

foreach ($path in $paths) {
    Write-Host "`nPermissions for: $path" -ForegroundColor Cyan
    icacls $path
}
```

### Required Permissions

| Path | Required Access | Purpose |
|------|----------------|---------|
| RootPath\inbox | Read, Write, Delete | Read files and delete after claiming |
| RootPath\processing | Full Control | Create subfolders, write files |
| RootPath\sent | Write, Modify | Move files after successful delivery |
| RootPath\failed | Write, Modify | Move files after permanent failure |
| Database directory | Read, Write, Modify | Read/write database file |
| Log directory | Write, Modify | Create and write log files |

### Granting Permissions

```powershell
# Grant permissions to service account
$serviceAccount = "NT AUTHORITY\SYSTEM"  # Or your custom account

# RootPath
icacls "C:\Data\FileDelivery" /grant "${serviceAccount}:(OI)(CI)M" /T

# Database directory
icacls "C:\ProgramData\FolderToApi" /grant "${serviceAccount}:(OI)(CI)M" /T

# Log directory
icacls "C:\Logs\FolderToApi" /grant "${serviceAccount}:(OI)(CI)M" /T

Write-Host "Permissions granted successfully" -ForegroundColor Green

# Restart service
Restart-Service FolderToApiService
```

---

## Log Analysis

### Finding Specific Information in Logs

#### Find All Errors

```powershell
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern '"Level":"Error"' | Select-Object -Last 20
```

#### Track a Specific Job

```powershell
$jobId = "abc123"  # Replace with your job ID
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern $jobId
```

#### Find HTTP Failures

```powershell
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern '"HttpStatus":[45]\d{2}' | Select-Object -Last 20
```

#### Count Events by Type

```powershell
$logFile = "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json"
Get-Content $logFile |
    ForEach-Object { (ConvertFrom-Json $_).Level } |
    Group-Object |
    Select-Object Name, Count |
    Sort-Object Count -Descending
```

---

## Getting Additional Help

### Information to Collect

When reporting issues, collect the following:

1. **Service version** and Windows version
2. **Recent Event Log entries:**
   ```powershell
   Get-EventLog -LogName Application -Source FolderToApiService -Newest 20 | Out-File C:\Temp\eventlog.txt
   ```

3. **Configuration** (redact API key):
   ```powershell
   Get-Content "C:\Program Files\FolderToApi\appsettings.json"
   ```

4. **Recent log files:**
   ```powershell
   Get-Content "C:\Logs\FolderToApi\log-*.json" -Tail 100 | Out-File C:\Temp\logs.txt
   ```

5. **Database state:**
   ```powershell
   sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT state, COUNT(*) FROM jobs GROUP BY state;" > C:\Temp\db-state.txt
   ```

6. **Folder state:**
   ```powershell
   Get-ChildItem "C:\Data\FileDelivery" -Recurse | Measure-Object | Out-File C:\Temp\folder-state.txt
   ```

### Next Steps

- Review [Operations Guide](operations.md) for routine procedures
- Check [FAQ](faq.md) for common questions
- Review [Configuration Reference](configuration.md) for settings
- Consult [Security Guide](security.md) for permission best practices

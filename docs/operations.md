# Operations Guide

Complete operational procedures for managing the Folder-to-API Delivery Service in production.

## Table of Contents

1. [Service Management](#service-management)
2. [Monitoring and Health Checks](#monitoring-and-health-checks)
3. [Log Review](#log-review)
4. [Managing Failed Jobs](#managing-failed-jobs)
5. [Configuration Updates](#configuration-updates)
6. [Backup and Recovery](#backup-and-recovery)
7. [Maintenance Tasks](#maintenance-tasks)
8. [Performance Monitoring](#performance-monitoring)
9. [Runbooks](#runbooks)

---

## Service Management

### Starting and Stopping

#### PowerShell Commands

```powershell
# Start the service
Start-Service FolderToApiService

# Stop the service
Stop-Service FolderToApiService

# Restart the service
Restart-Service FolderToApiService

# Check service status
Get-Service FolderToApiService

# View detailed service information
Get-Service FolderToApiService | Format-List *
```

#### Services Console (GUI)

1. Press `Win + R`, type `services.msc`, press Enter
2. Find "Folder to API Delivery Service"
3. Right-click for Start/Stop/Restart options
4. Double-click to view/modify service properties

#### Command Line (sc.exe)

```cmd
REM Start service
sc start FolderToApiService

REM Stop service
sc stop FolderToApiService

REM Query status
sc query FolderToApiService

REM Query configuration
sc qc FolderToApiService

REM Query failure actions
sc qfailure FolderToApiService
```

### Service Status Meanings

| Status | Description | Action |
|--------|-------------|--------|
| Running | Service is active and processing files | Normal operation |
| Stopped | Service is not running | Start if needed |
| Starting | Service is initializing | Wait momentarily |
| Stopping | Service is shutting down gracefully | Wait momentarily |
| Paused | Service is suspended (rare) | Resume or restart |

### Automatic Startup Configuration

The service is configured for automatic startup by default. To verify or change:

```powershell
# View startup type
Get-Service FolderToApiService | Select-Object Name, StartType, Status

# Set to automatic startup
Set-Service -Name FolderToApiService -StartupType Automatic

# Set to manual startup
Set-Service -Name FolderToApiService -StartupType Manual

# Set to disabled
Set-Service -Name FolderToApiService -StartupType Disabled
```

### Service Recovery Options

View configured recovery actions:

```powershell
sc qfailure FolderToApiService
```

**Default Recovery Actions:**
1. First failure: Restart the service (delay: 1 minute)
2. Second failure: Restart the service (delay: 1 minute)
3. Subsequent failures: Restart the service (delay: 5 minutes)

To modify recovery actions (requires Administrator):

```powershell
sc failure FolderToApiService reset= 86400 actions= restart/60000/restart/60000/restart/300000
```

---

## Monitoring and Health Checks

### Quick Health Check

Run this PowerShell script for a comprehensive health check:

```powershell
# Health Check Script
Write-Host "=== Folder-to-API Service Health Check ===" -ForegroundColor Cyan

# 1. Service Status
Write-Host "`n[1/5] Service Status..." -ForegroundColor Yellow
$service = Get-Service FolderToApiService -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "  Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') { 'Green' } else { 'Red' })
    $process = Get-Process -Name "FolderToApi.Service" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "  CPU: $([math]::Round($process.CPU, 2))s" -ForegroundColor Green
        Write-Host "  Memory: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB" -ForegroundColor Green
        Write-Host "  Started: $($process.StartTime)" -ForegroundColor Green
    }
} else {
    Write-Host "  Service not found!" -ForegroundColor Red
}

# 2. Folder Status
Write-Host "`n[2/5] Folder Status..." -ForegroundColor Yellow
$rootPath = "C:\Data\FileDelivery"  # Update to your RootPath
if (Test-Path $rootPath) {
    $inbox = Get-ChildItem "$rootPath\inbox" -ErrorAction SilentlyContinue | Measure-Object
    $processing = Get-ChildItem "$rootPath\processing" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object
    $sent = Get-ChildItem "$rootPath\sent" -ErrorAction SilentlyContinue | Measure-Object
    $failed = Get-ChildItem "$rootPath\failed" -ErrorAction SilentlyContinue | Measure-Object

    Write-Host "  Inbox: $($inbox.Count) files" -ForegroundColor $(if ($inbox.Count -gt 0) { 'Yellow' } else { 'Green' })
    Write-Host "  Processing: $($processing.Count) files" -ForegroundColor $(if ($processing.Count -gt 10) { 'Yellow' } else { 'Green' })
    Write-Host "  Sent: $($sent.Count) files" -ForegroundColor Green
    Write-Host "  Failed: $($failed.Count) files" -ForegroundColor $(if ($failed.Count -gt 0) { 'Red' } else { 'Green' })
} else {
    Write-Host "  Root path not accessible!" -ForegroundColor Red
}

# 3. Recent Events
Write-Host "`n[3/5] Recent Events..." -ForegroundColor Yellow
$recentEvents = Get-EventLog -LogName Application -Source FolderToApiService -Newest 5 -ErrorAction SilentlyContinue
if ($recentEvents) {
    $recentEvents | ForEach-Object {
        $color = switch ($_.EntryType) {
            'Error' { 'Red' }
            'Warning' { 'Yellow' }
            default { 'White' }
        }
        Write-Host "  $($_.TimeGenerated) [$($_.EntryType)]: $($_.Message.Substring(0, [Math]::Min(80, $_.Message.Length)))" -ForegroundColor $color
    }
} else {
    Write-Host "  No recent events" -ForegroundColor Gray
}

# 4. Database Status
Write-Host "`n[4/5] Database Status..." -ForegroundColor Yellow
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"  # Update to your DB path
if (Test-Path $dbPath) {
    $dbInfo = Get-Item $dbPath
    Write-Host "  Size: $([math]::Round($dbInfo.Length / 1MB, 2)) MB" -ForegroundColor Green
    Write-Host "  Last Modified: $($dbInfo.LastWriteTime)" -ForegroundColor Green
} else {
    Write-Host "  Database not found!" -ForegroundColor Red
}

# 5. Log Files
Write-Host "`n[5/5] Log Files..." -ForegroundColor Yellow
$logDir = "C:\Logs\FolderToApi"  # Update to your log directory
if (Test-Path $logDir) {
    $todayLog = Get-Item "$logDir\log-$(Get-Date -Format 'yyyyMMdd').json" -ErrorAction SilentlyContinue
    if ($todayLog) {
        Write-Host "  Today's log: $([math]::Round($todayLog.Length / 1MB, 2)) MB" -ForegroundColor Green
    }
    $allLogs = Get-ChildItem "$logDir\log-*.json" | Measure-Object -Property Length -Sum
    Write-Host "  Total logs: $($allLogs.Count) files, $([math]::Round($allLogs.Sum / 1MB, 2)) MB" -ForegroundColor Green
} else {
    Write-Host "  Log directory not found!" -ForegroundColor Red
}

Write-Host "`n=== Health Check Complete ===" -ForegroundColor Cyan
```

### Key Metrics to Monitor

| Metric | Command | Normal Range | Alert Threshold |
|--------|---------|--------------|-----------------|
| Service Status | `Get-Service FolderToApiService` | Running | Not Running |
| Inbox Files | `(Get-ChildItem "$rootPath\inbox").Count` | 0-10 | > 50 |
| Processing Files | `(Get-ChildItem "$rootPath\processing" -Recurse -File).Count` | 0-5 | > 20 |
| Failed Files | `(Get-ChildItem "$rootPath\failed").Count` | 0 | > 0 |
| CPU Usage | `(Get-Process "FolderToApi.Service").CPU` | < 5% average | > 50% sustained |
| Memory Usage | `(Get-Process "FolderToApi.Service").WorkingSet64 / 1MB` | 50-200 MB | > 500 MB |
| Database Size | `(Get-Item $dbPath).Length / 1MB` | 1-100 MB | > 500 MB |

### Automated Health Monitoring

Create a scheduled task to run health checks periodically:

```powershell
# Create scheduled task for health monitoring
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\Scripts\HealthCheck.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At "09:00AM"
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopOnIdleEnd

Register-ScheduledTask -TaskName "FolderToApi-HealthCheck" -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Daily health check for Folder-to-API service"
```

---

## Log Review

### Log Locations

| Log Type | Location | Format |
|----------|----------|--------|
| Windows Event Log | Application → FolderToApiService | Structured text |
| JSON Log Files | `C:\Logs\FolderToApi\log-*.json` | JSON |
| Service Output | Captured in Event Log | Text |

### Viewing Windows Event Log

```powershell
# View all service events
Get-EventLog -LogName Application -Source FolderToApiService

# View recent events (last 20)
Get-EventLog -LogName Application -Source FolderToApiService -Newest 20

# View only errors
Get-EventLog -LogName Application -Source FolderToApiService -EntryType Error -Newest 10

# View only warnings
Get-EventLog -LogName Application -Source FolderToApiService -EntryType Warning -Newest 10

# View events since specific time
Get-EventLog -LogName Application -Source FolderToApiService -After (Get-Date).AddHours(-1)

# Export to CSV
Get-EventLog -LogName Application -Source FolderToApiService -Newest 100 | Export-Csv -Path "C:\Temp\service-events.csv"
```

### Viewing JSON Log Files

```powershell
# View today's log
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json"

# Tail log file (follow new entries)
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Wait -Tail 50

# View last 20 lines
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Tail 20

# Search for errors in today's log
Select-String -Path "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Pattern '"Level":"Error"'

# Search for specific job ID across all logs
Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern "JOB123456"

# Parse JSON and filter (PowerShell 7+)
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" |
    ForEach-Object { ConvertFrom-Json $_ } |
    Where-Object { $_.Level -eq "Error" } |
    Select-Object Timestamp, Message, Exception

# Count log entries by level
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" |
    ForEach-Object { (ConvertFrom-Json $_).Level } |
    Group-Object |
    Select-Object Name, Count
```

### Common Log Patterns to Monitor

**Successful File Processing:**
```json
{"Timestamp":"2026-02-09T10:30:00","Level":"Information","MessageTemplate":"File delivered successfully","Properties":{"JobId":"abc123","FileName":"document.pdf","HttpStatus":200}}
```

**Retry Scheduled:**
```json
{"Timestamp":"2026-02-09T10:31:00","Level":"Warning","MessageTemplate":"API delivery failed, will retry","Properties":{"JobId":"abc123","HttpStatus":503,"NextAttempt":"2026-02-09T10:31:30"}}
```

**Dead Letter:**
```json
{"Timestamp":"2026-02-09T11:00:00","Level":"Error","MessageTemplate":"Job moved to dead-letter","Properties":{"JobId":"abc123","Reason":"MaxAttemptsExceeded","Attempts":10}}
```

---

## Managing Failed Jobs

### Listing Failed Jobs

```powershell
# List all files in failed folder
Get-ChildItem "C:\Data\FileDelivery\failed" | Format-Table Name, Length, LastWriteTime

# Count failed files
(Get-ChildItem "C:\Data\FileDelivery\failed").Count

# Get details of largest failed files
Get-ChildItem "C:\Data\FileDelivery\failed" | Sort-Object Length -Descending | Select-Object -First 10 | Format-Table Name, @{Label="Size (MB)";Expression={[math]::Round($_.Length/1MB,2)}}, LastWriteTime
```

### Querying Database for Error Details

If you have SQLite CLI installed:

```powershell
# Install SQLite CLI (if needed)
# Download from https://www.sqlite.org/download.html

# Query failed jobs
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT job_id, source_path, last_error, last_http_status, attempt_count FROM jobs WHERE state = 'Failed' ORDER BY last_attempt DESC LIMIT 20;"

# Count jobs by state
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT state, COUNT(*) as count FROM jobs GROUP BY state;"

# Find jobs with specific error
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT * FROM jobs WHERE last_error LIKE '%timeout%';"
```

Alternatively, use [DB Browser for SQLite](https://sqlitebrowser.org/) for a GUI experience.

### Retrying Failed Jobs

#### Manual Retry Process

1. **Identify the root cause** by reviewing logs and database error details
2. **Fix the underlying issue** (e.g., correct API endpoint, restore network connectivity)
3. **Move file back to inbox** for reprocessing:

```powershell
# Move a specific failed file back to inbox for retry
$failedFile = "C:\Data\FileDelivery\failed\document.pdf"
$inboxPath = "C:\Data\FileDelivery\inbox\"

if (Test-Path $failedFile) {
    Move-Item -Path $failedFile -Destination $inboxPath
    Write-Host "File moved to inbox for retry: $failedFile" -ForegroundColor Green
} else {
    Write-Host "File not found: $failedFile" -ForegroundColor Red
}
```

4. **Clean up database record** (optional, service will create new job):

```powershell
# Delete old failed job record (use job_id from database query)
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "DELETE FROM jobs WHERE job_id = 'abc123';"
```

#### Bulk Retry Process

```powershell
# Move all failed files back to inbox
$failedFolder = "C:\Data\FileDelivery\failed"
$inboxFolder = "C:\Data\FileDelivery\inbox"

Get-ChildItem $failedFolder | ForEach-Object {
    Move-Item -Path $_.FullName -Destination $inboxFolder
    Write-Host "Moved: $($_.Name)" -ForegroundColor Green
}

Write-Host "All failed files moved to inbox for retry" -ForegroundColor Cyan
```

### Archiving or Deleting Failed Jobs

If files are truly unrecoverable:

```powershell
# Archive failed files to a different location
$archivePath = "C:\Archives\FailedFiles\$(Get-Date -Format 'yyyy-MM-dd')"
New-Item -ItemType Directory -Path $archivePath -Force
Move-Item -Path "C:\Data\FileDelivery\failed\*" -Destination $archivePath

# OR permanently delete failed files (use with caution!)
# Remove-Item -Path "C:\Data\FileDelivery\failed\*" -Force
```

---

## Configuration Updates

### Safe Configuration Update Process

1. **Backup current configuration:**
   ```powershell
   Copy-Item "C:\Program Files\FolderToApi\appsettings.json" "C:\Program Files\FolderToApi\appsettings.json.backup-$(Get-Date -Format 'yyyyMMddHHmmss')"
   ```

2. **Edit configuration file:**
   ```powershell
   notepad "C:\Program Files\FolderToApi\appsettings.json"
   ```

3. **Validate JSON syntax:**
   ```powershell
   try {
       Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json | Out-Null
       Write-Host "Configuration file is valid JSON" -ForegroundColor Green
   } catch {
       Write-Host "Configuration file has invalid JSON syntax!" -ForegroundColor Red
       Write-Host $_.Exception.Message
   }
   ```

4. **Restart service:**
   ```powershell
   Restart-Service FolderToApiService
   ```

5. **Verify startup:**
   ```powershell
   # Wait a few seconds for startup
   Start-Sleep -Seconds 5

   # Check service status
   Get-Service FolderToApiService

   # Check recent events for errors
   Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
   ```

6. **Rollback if needed:**
   ```powershell
   # If service failed to start, restore backup
   Stop-Service FolderToApiService -Force -ErrorAction SilentlyContinue
   Copy-Item "C:\Program Files\FolderToApi\appsettings.json.backup-*" "C:\Program Files\FolderToApi\appsettings.json"
   Start-Service FolderToApiService
   ```

### Common Configuration Changes

#### Update API Endpoint

```powershell
# Read current config
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json

# Update endpoint
$config.ApiClient.EndpointUrl = "https://new-api.example.com/upload"

# Save and restart
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

#### Adjust Scan Interval

```powershell
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
$config.FolderWatcher.ScanIntervalSeconds = 60
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

#### Change Log Level

```powershell
$config = Get-Content "C:\Program Files\FolderToApi\appsettings.json" | ConvertFrom-Json
$config.Logging.MinimumLevel = "Debug"  # or "Information", "Warning", "Error"
$config | ConvertTo-Json -Depth 10 | Set-Content "C:\Program Files\FolderToApi\appsettings.json"
Restart-Service FolderToApiService
```

---

## Backup and Recovery

### What to Back Up

| Item | Location | Frequency | Importance |
|------|----------|-----------|------------|
| Configuration Files | `C:\Program Files\FolderToApi\appsettings*.json` | Before changes | Critical |
| SQLite Database | `C:\ProgramData\FolderToApi\foldertoapi.db` | Daily | Critical |
| Processing Files | `C:\Data\FileDelivery\processing\` | Daily | High |
| Failed Files | `C:\Data\FileDelivery\failed\` | Weekly | Medium |
| Log Files | `C:\Logs\FolderToApi\` | Weekly | Low |

### Backup Script

```powershell
# Backup script - Run daily via scheduled task
$backupRoot = "C:\Backups\FolderToApi\$(Get-Date -Format 'yyyy-MM-dd')"
New-Item -ItemType Directory -Path $backupRoot -Force

# Backup configuration
Copy-Item "C:\Program Files\FolderToApi\appsettings*.json" $backupRoot

# Backup database (stop service first for consistency)
Stop-Service FolderToApiService
Copy-Item "C:\ProgramData\FolderToApi\foldertoapi.db" $backupRoot
Copy-Item "C:\ProgramData\FolderToApi\foldertoapi.db-shm" $backupRoot -ErrorAction SilentlyContinue
Copy-Item "C:\ProgramData\FolderToApi\foldertoapi.db-wal" $backupRoot -ErrorAction SilentlyContinue
Start-Service FolderToApiService

# Backup processing files
Copy-Item -Path "C:\Data\FileDelivery\processing" -Destination $backupRoot -Recurse

Write-Host "Backup completed: $backupRoot" -ForegroundColor Green
```

### Recovery Process

#### Restore from Backup

```powershell
# 1. Stop service
Stop-Service FolderToApiService

# 2. Restore configuration
$backupDate = "2026-02-08"  # Update to your backup date
Copy-Item "C:\Backups\FolderToApi\$backupDate\appsettings.json" "C:\Program Files\FolderToApi\" -Force

# 3. Restore database
Copy-Item "C:\Backups\FolderToApi\$backupDate\foldertoapi.db" "C:\ProgramData\FolderToApi\" -Force

# 4. Restore processing files (if needed)
Remove-Item "C:\Data\FileDelivery\processing\*" -Recurse -Force
Copy-Item -Path "C:\Backups\FolderToApi\$backupDate\processing\*" -Destination "C:\Data\FileDelivery\processing\" -Recurse

# 5. Start service
Start-Service FolderToApiService

# 6. Verify
Get-Service FolderToApiService
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

#### Disaster Recovery (Full Reinstall)

1. **Stop and uninstall service** (if still present)
   ```powershell
   .\deployment\Uninstall-Service.ps1 -RemoveFiles
   ```

2. **Restore configuration and database** from backup

3. **Reinstall service**
   ```powershell
   .\deployment\Install-Service.ps1 -SourcePath "C:\Temp\publish"
   ```

4. **Restore processing files** (if any)

5. **Verify operation**

---

## Maintenance Tasks

### Daily Tasks

- [ ] Check service status: `Get-Service FolderToApiService`
- [ ] Review failed files: `Get-ChildItem "C:\Data\FileDelivery\failed"`
- [ ] Monitor inbox backlog: `Get-ChildItem "C:\Data\FileDelivery\inbox"`

### Weekly Tasks

- [ ] Review Event Log for errors and warnings
- [ ] Check disk space on root path and log directory
- [ ] Review database size growth trend
- [ ] Validate backup completion

### Monthly Tasks

- [ ] Review and action failed jobs
- [ ] Analyze performance metrics (throughput, latency)
- [ ] Review and optimize configuration settings
- [ ] Update documentation for any configuration changes
- [ ] Test disaster recovery procedures

### Quarterly Tasks

- [ ] Review and test service recovery options
- [ ] Audit service account permissions
- [ ] Review log retention policies
- [ ] Plan for capacity upgrades if needed

---

## Performance Monitoring

### CPU and Memory Usage

```powershell
# Current resource usage
Get-Process -Name "FolderToApi.Service" | Format-Table Name, CPU, @{Label="Memory (MB)";Expression={[math]::Round($_.WorkingSet64/1MB,2)}}, StartTime

# Monitor resource usage over time (sample every 5 seconds for 1 minute)
1..12 | ForEach-Object {
    $proc = Get-Process -Name "FolderToApi.Service" -ErrorAction SilentlyContinue
    if ($proc) {
        [PSCustomObject]@{
            Time = Get-Date -Format "HH:mm:ss"
            CPU = [math]::Round($proc.CPU, 2)
            MemoryMB = [math]::Round($proc.WorkingSet64 / 1MB, 2)
            Threads = $proc.Threads.Count
        }
    }
    Start-Sleep -Seconds 5
} | Format-Table -AutoSize
```

### Throughput Metrics

```powershell
# Count files processed today
$today = Get-Date -Format "yyyy-MM-dd"
$sentToday = Get-ChildItem "C:\Data\FileDelivery\sent" | Where-Object { $_.LastWriteTime.Date -eq (Get-Date).Date }
Write-Host "Files processed today: $($sentToday.Count)" -ForegroundColor Green

# Calculate average processing time (from log analysis)
# This requires parsing JSON logs for job start and complete timestamps
```

### Database Growth

```powershell
# Monitor database size over time
$dbPath = "C:\ProgramData\FolderToApi\foldertoapi.db"
$dbInfo = Get-Item $dbPath
Write-Host "Database size: $([math]::Round($dbInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan

# Count total jobs in database
sqlite3 $dbPath "SELECT COUNT(*) as total_jobs FROM jobs;"

# Count jobs by state
sqlite3 $dbPath "SELECT state, COUNT(*) FROM jobs GROUP BY state;"
```

---

## Runbooks

### Runbook: Service Won't Start

**Symptoms:** Service fails to start, goes to "Stopped" state immediately.

**Steps:**

1. Check Event Log for startup errors:
   ```powershell
   Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
   ```

2. Common causes and fixes:
   - **Configuration error**: Validate JSON syntax
   - **Missing RootPath**: Verify path exists and is accessible
   - **Permission denied**: Check service account permissions
   - **Database locked**: Ensure no other process is using the database

3. If still failing, review detailed logs:
   ```powershell
   Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Tail 50
   ```

4. Try starting interactively for detailed error output (development only):
   ```powershell
   cd "C:\Program Files\FolderToApi"
   .\FolderToApi.Service.exe
   ```

---

### Runbook: High Backlog in Inbox

**Symptoms:** Many files accumulating in inbox folder, not being processed.

**Steps:**

1. Verify service is running:
   ```powershell
   Get-Service FolderToApiService
   ```

2. Check for locked files:
   ```powershell
   Get-ChildItem "C:\Data\FileDelivery\inbox" | ForEach-Object {
       try {
           [IO.File]::Open($_.FullName, 'Open', 'Read', 'None').Close()
           Write-Host "$($_.Name) - OK" -ForegroundColor Green
       } catch {
           Write-Host "$($_.Name) - LOCKED" -ForegroundColor Red
       }
   }
   ```

3. Review logs for scanning errors:
   ```powershell
   Select-String -Path "C:\Logs\FolderToApi\log-*.json" -Pattern "ScanLoopHostedService|Error" | Select-Object -Last 20
   ```

4. Check API connectivity:
   ```powershell
   Test-NetConnection -ComputerName api.example.com -Port 443
   ```

5. Review processing folder for stuck jobs:
   ```powershell
   Get-ChildItem "C:\Data\FileDelivery\processing" -Recurse -File
   ```

---

### Runbook: API Delivery Failures

**Symptoms:** Files moving to failed folder, API errors in logs.

**Steps:**

1. Check failed folder:
   ```powershell
   Get-ChildItem "C:\Data\FileDelivery\failed" | Format-Table Name, Length, LastWriteTime
   ```

2. Query database for error details:
   ```powershell
   sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT job_id, source_path, last_error, last_http_status FROM jobs WHERE state = 'Failed' ORDER BY last_attempt DESC LIMIT 10;"
   ```

3. Common issues:
   - **401/403**: Invalid or expired API key
   - **404**: Incorrect endpoint URL
   - **500/503**: API service down or overloaded
   - **Timeout**: Increase `RequestTimeoutSeconds` or check network latency

4. Test API endpoint manually:
   ```powershell
   Invoke-WebRequest -Uri "https://api.example.com" -Headers @{"X-API-Key"="test-key"} -Method POST
   ```

5. After fixing root cause, retry failed files (see "Managing Failed Jobs" section above)

---

### Runbook: Rotate API Key

**Steps:**

1. Obtain new API key from API provider

2. Update stored key (if using Credential Manager):
   ```powershell
   cmdkey /delete:FolderToApiService
   cmdkey /generic:FolderToApiService /user:ApiKey /pass:new-api-key-here
   ```

3. Or update configuration (if using Configuration source):
   ```powershell
   # Edit appsettings.json and update ApiKey value
   notepad "C:\Program Files\FolderToApi\appsettings.json"
   ```

4. Restart service:
   ```powershell
   Restart-Service FolderToApiService
   ```

5. Verify successful API calls in logs:
   ```powershell
   Get-EventLog -LogName Application -Source FolderToApiService -Newest 10
   ```

---

## Next Steps

- **[Troubleshooting Guide](troubleshooting.md)** - Detailed solutions for common issues
- **[Security Guide](security.md)** - Best practices for securing the service
- **[Configuration Reference](configuration.md)** - Complete configuration options

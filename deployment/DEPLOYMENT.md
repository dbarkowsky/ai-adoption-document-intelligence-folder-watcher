# FolderToApiService - Windows Service Deployment Guide

This guide provides step-by-step instructions for deploying the FolderToApiService as a Windows Service on Windows Server.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Publishing the Application](#publishing-the-application)
4. [Installing the Service](#installing-the-service)
5. [Configuration](#configuration)
6. [Service Management](#service-management)
7. [Uninstalling the Service](#uninstalling-the-service)
8. [Troubleshooting](#troubleshooting)
9. [Advanced Configuration](#advanced-configuration)

---

## Prerequisites

### Development Machine
- .NET 10.0 SDK or later
- PowerShell 5.1 or later
- Git (optional, for cloning the repository)

### Target Windows Server
- Windows Server 2016 or later (2019/2022 recommended)
- PowerShell 5.1 or later (included in Windows Server)
- Administrator access
- Network connectivity to the target API endpoint

### Service Account Requirements
The service account needs:
- **Read/Write permissions** on the RootPath folder (inbox, processing, sent, failed subfolders)
- **Read/Write permissions** on the database file location
- **Read/Write permissions** on the log directory
- **Network access** to reach the API endpoint URL
- **Logon as a service** right (automatically granted by Windows when creating service)

---

## Quick Start

For experienced administrators, here's the minimal deployment flow:

```powershell
# 1. Publish the application
.\deployment\Publish-Service.ps1

# 2. Copy the publish folder to the target server

# 3. On the target server, configure appsettings.json
notepad "C:\Temp\publish\appsettings.json"

# 4. Install the service
.\deployment\Install-Service.ps1 -SourcePath "C:\Temp\publish"
```

---

## Publishing the Application

### Step 1: Build the Single-File Executable

From the repository root, run the publish script:

```powershell
.\deployment\Publish-Service.ps1
```

This creates a self-contained, single-file executable in `.\deployment\publish\`.

#### Publish Options

```powershell
# Publish for 64-bit Windows (default)
.\deployment\Publish-Service.ps1 -Runtime win-x64

# Publish with debug symbols
.\deployment\Publish-Service.ps1 -IncludeSymbols

# Publish to custom directory
.\deployment\Publish-Service.ps1 -OutputPath "C:\Deploy\FolderToApi"

# Publish as framework-dependent (requires .NET runtime on target)
.\deployment\Publish-Service.ps1 -NoSelfContained
```

### Step 2: Verify Published Files

The publish folder should contain:

```
publish/
├── FolderToApi.Service.exe    (main executable, ~80-100 MB)
├── appsettings.json            (default configuration)
├── appsettings.Development.json
└── appsettings.Production.json
```

### Step 3: Transfer to Target Server

Copy the entire `publish` folder to the target Windows Server, for example:

- Via RDP: Copy/paste or map a network drive
- Via network share: `\\server\share\FolderToApi\`
- Via CI/CD pipeline: automated deployment

---

## Installing the Service

### Step 1: Prepare Configuration

**IMPORTANT:** Configure the application before installation.

Edit `appsettings.json` in the publish folder:

```json
{
  "FolderWatcher": {
    "RootPath": "C:\\Data\\FileDelivery",  // UPDATE THIS
    "ScanIntervalSeconds": 30
  },
  "ApiClient": {
    "EndpointUrl": "https://api.example.com/upload",  // UPDATE THIS
    "ApiKey": "your-api-key-here"  // UPDATE THIS or use Credential Manager
  },
  "Database": {
    "ConnectionString": "Data Source=C:\\ProgramData\\FolderToApi\\foldertoapi.db"
  },
  "Logging": {
    "LogDirectory": "C:\\Logs\\FolderToApi"
  }
}
```

### Step 2: Run Installation Script

Open PowerShell **as Administrator** and run:

```powershell
cd C:\Temp\publish
.\Install-Service.ps1 -SourcePath "C:\Temp\publish"
```

The script will:
1. ✓ Validate configuration files
2. ✓ Create installation directory (`C:\Program Files\FolderToApi`)
3. ✓ Copy executable and configuration files
4. ✓ Create required directories (logs, database)
5. ✓ Create Windows Service
6. ✓ Configure automatic startup
7. ✓ Configure recovery options (restart on failure)
8. ✓ Start the service

### Step 3: Verify Installation

```powershell
# Check service status
Get-Service FolderToApiService

# Check recent log entries
Get-EventLog -LogName Application -Source FolderToApiService -Newest 10

# View log files
Get-Content "C:\Logs\FolderToApi\log-20260209.json" -Tail 20
```

---

## Configuration

### Configuration File Locations

After installation:
- Main config: `C:\Program Files\FolderToApi\appsettings.json`
- Production config: `C:\Program Files\FolderToApi\appsettings.Production.json`

### Critical Settings

#### 1. RootPath (Required)

```json
"FolderWatcher": {
  "RootPath": "C:\\Data\\FileDelivery"
}
```

The service will create subfolders:
- `inbox/` - Place files here for delivery
- `processing/` - Files being processed
- `sent/` - Successfully delivered files
- `failed/` - Failed delivery attempts

Supports UNC paths: `"\\\\server\\share\\FileDelivery"`

#### 2. API Endpoint (Required)

```json
"ApiClient": {
  "EndpointUrl": "https://api.example.com/upload",
  "ApiKeyHeaderName": "X-API-Key"
}
```

#### 3. API Key Configuration

**Option A: Configuration File (Simple)**

```json
"ApiClient": {
  "ApiKeySource": "Configuration",
  "ApiKey": "your-api-key-here"
}
```

**Option B: Windows Credential Manager (Recommended)**

```json
"ApiClient": {
  "ApiKeySource": "WindowsCredentialManager",
  "ApiKey": ""  // Leave empty
}
```

Store the API key securely:

```powershell
# Store credential for the service account
cmdkey /generic:FolderToApiService /user:ApiKey /pass:your-actual-api-key
```

#### 4. Scan and Retry Settings

```json
"FolderWatcher": {
  "ScanIntervalSeconds": 30,      // How often to check inbox
  "StableAgeSeconds": 10,          // Wait time for file to stop changing
  "AllowedExtensions": [".pdf", ".docx"],
  "MaxFileSizeMB": 50
},
"RetryPolicy": {
  "MaxAttempts": 10,               // Retry up to 10 times
  "MaxJobAgeDays": 7,              // Give up after 7 days
  "InitialDelaySeconds": 10,       // First retry after 10 seconds
  "MaxDelaySeconds": 3600          // Max 1 hour between retries
}
```

### Applying Configuration Changes

After modifying configuration:

```powershell
# Restart the service to apply changes
Restart-Service FolderToApiService

# Verify service restarted successfully
Get-Service FolderToApiService
```

---

## Service Management

### Using PowerShell

```powershell
# Start the service
Start-Service FolderToApiService

# Stop the service
Stop-Service FolderToApiService

# Restart the service
Restart-Service FolderToApiService

# Check service status
Get-Service FolderToApiService

# View service details
Get-Service FolderToApiService | Format-List *
```

### Using Services Console

1. Press `Win + R`, type `services.msc`, press Enter
2. Find "Folder to API Delivery Service"
3. Right-click for Start/Stop/Restart options
4. Double-click to view/modify properties

### Using Command Line (sc.exe)

```cmd
REM Start service
sc start FolderToApiService

REM Stop service
sc stop FolderToApiService

REM Query service status
sc query FolderToApiService

REM View service configuration
sc qc FolderToApiService
```

### Monitoring and Logs

#### Windows Event Log

```powershell
# View recent service events
Get-EventLog -LogName Application -Source FolderToApiService -Newest 20

# Monitor events in real-time
Get-EventLog -LogName Application -Source FolderToApiService -Newest 1 -After (Get-Date).AddMinutes(-5)
```

#### Application Log Files

```powershell
# View today's JSON log
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json"

# Tail log file (follow new entries)
Get-Content "C:\Logs\FolderToApi\log-$(Get-Date -Format 'yyyyMMdd').json" -Wait -Tail 50

# Search for errors in logs
Select-String -Path "C:\Logs\FolderToApi\*.json" -Pattern '"Level":"Error"' | Select-Object -Last 10
```

#### Performance Metrics

```powershell
# Check service process
Get-Process -Name "FolderToApi.Service" | Format-Table Name, CPU, WorkingSet, StartTime

# Monitor folder sizes
Get-ChildItem "C:\Data\FileDelivery" -Recurse | Measure-Object -Property Length -Sum
```

---

## Uninstalling the Service

### Option 1: Uninstall Only (Keep Files)

```powershell
.\deployment\Uninstall-Service.ps1
```

This stops and removes the service registration but leaves files in place.

### Option 2: Complete Removal (Including Files)

```powershell
.\deployment\Uninstall-Service.ps1 -RemoveFiles
```

This removes the service and deletes the installation directory.

### Option 3: Full Cleanup (Including Data)

```powershell
.\deployment\Uninstall-Service.ps1 -RemoveFiles -RemoveData -Force
```

⚠️ **WARNING:** This deletes the database and log files. Use with caution!

---

## Troubleshooting

### Service Won't Start

#### Check Event Log

```powershell
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

Common errors:
- **Configuration validation error**: Check `appsettings.json` for missing/invalid settings
- **Access denied**: Service account lacks permissions on RootPath or database
- **File not found**: RootPath or database directory doesn't exist

#### Validate Configuration

```powershell
.\deployment\Install-Service.ps1 -ValidateOnly
```

#### Check Permissions

```powershell
# Test file system access
icacls "C:\Data\FileDelivery"
icacls "C:\Logs\FolderToApi"
icacls "C:\ProgramData\FolderToApi"
```

### Files Not Being Processed

#### Check Inbox

```powershell
# List files in inbox
Get-ChildItem "C:\Data\FileDelivery\inbox"

# Check file locks (files in use)
Get-ChildItem "C:\Data\FileDelivery\inbox" | ForEach-Object {
    try {
        [IO.File]::Open($_.FullName, 'Open', 'Read', 'None').Close()
        Write-Host "$($_.Name) - Not locked" -ForegroundColor Green
    } catch {
        Write-Host "$($_.Name) - LOCKED" -ForegroundColor Red
    }
}
```

#### Check Service Status

```powershell
Get-Service FolderToApiService
Get-Process -Name "FolderToApi.Service"
```

#### Review Logs

```powershell
# Look for scan loop activity
Select-String -Path "C:\Logs\FolderToApi\*.json" -Pattern "ScanLoopHostedService"

# Look for errors
Select-String -Path "C:\Logs\FolderToApi\*.json" -Pattern '"Level":"Error"'
```

### API Delivery Failures

#### Check Failed Folder

```powershell
Get-ChildItem "C:\Data\FileDelivery\failed"
```

#### Check Database for Error Details

```powershell
# Install SQLite CLI tool or use DB Browser for SQLite
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT * FROM jobs WHERE state = 'Failed' ORDER BY last_attempt DESC LIMIT 10;"
```

#### Test API Connectivity

```powershell
# Test basic connectivity
Test-NetConnection -ComputerName api.example.com -Port 443

# Test HTTPS endpoint
Invoke-WebRequest -Uri "https://api.example.com" -UseBasicParsing
```

### High Memory or CPU Usage

```powershell
# Check resource usage
Get-Process -Name "FolderToApi.Service" | Format-Table Name, CPU, WorkingSet, PrivateMemorySize

# Check backlog size
Get-ChildItem "C:\Data\FileDelivery\inbox" | Measure-Object
Get-ChildItem "C:\Data\FileDelivery\processing" | Measure-Object
```

If there's a large backlog, consider:
- Increasing `ScanIntervalSeconds` to reduce CPU usage
- Checking API performance (slow uploads may cause backlog)
- Reviewing `MaxFileSizeMB` to exclude oversized files

### Service Keeps Crashing

Check recovery actions:

```powershell
sc qfailure FolderToApiService
```

View crash logs:

```powershell
# Recent crashes
Get-EventLog -LogName Application -Source FolderToApiService -EntryType Error -Newest 10

# Application crash logs
Get-EventLog -LogName Application | Where-Object { $_.Message -like "*FolderToApi*" -and $_.EntryType -eq "Error" }
```

---

## Advanced Configuration

### Custom Service Account

Install with a domain service account:

```powershell
.\deployment\Install-Service.ps1 `
    -ServiceAccount "Custom" `
    -CustomServiceAccount "DOMAIN\ServiceAccount" `
    -ServicePassword (ConvertTo-SecureString "Password123" -AsPlainText -Force)
```

Grant permissions to the service account:

```powershell
# File system permissions
icacls "C:\Data\FileDelivery" /grant "DOMAIN\ServiceAccount:(OI)(CI)M"
icacls "C:\Logs\FolderToApi" /grant "DOMAIN\ServiceAccount:(OI)(CI)M"
icacls "C:\ProgramData\FolderToApi" /grant "DOMAIN\ServiceAccount:(OI)(CI)M"
```

### UNC Path Configuration

For network shares:

```json
"FolderWatcher": {
  "RootPath": "\\\\fileserver\\shared\\FileDelivery"
}
```

Ensure the service account has network access:
- Use a domain account (not LocalSystem)
- Grant Read/Write permissions on the network share
- Test connectivity: `Test-Path "\\fileserver\shared"`

### Multiple Instances

To run multiple instances with different configurations:

```powershell
# Install first instance
.\deployment\Install-Service.ps1 `
    -ServiceName "FolderToApiService1" `
    -InstallPath "C:\Program Files\FolderToApi1" `
    -SourcePath "C:\Temp\publish1"

# Install second instance
.\deployment\Install-Service.ps1 `
    -ServiceName "FolderToApiService2" `
    -InstallPath "C:\Program Files\FolderToApi2" `
    -SourcePath "C:\Temp\publish2"
```

Ensure each instance has:
- Unique service name
- Unique installation path
- Unique RootPath, database, and log directories

### SSL/TLS Configuration

The service uses the system's SSL/TLS settings. To enforce TLS 1.2+:

```powershell
# Registry keys for TLS 1.2 (run as Administrator)
New-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' -Name 'DisabledByDefault' -Value 0 -PropertyType 'DWORD' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' -Name 'Enabled' -Value 1 -PropertyType 'DWORD' -Force
```

### Firewall Configuration

If the service needs outbound HTTPS access through a firewall:

```powershell
# Allow outbound HTTPS
New-NetFirewallRule -DisplayName "FolderToApiService Outbound HTTPS" `
    -Direction Outbound `
    -Program "C:\Program Files\FolderToApi\FolderToApi.Service.exe" `
    -Protocol TCP `
    -LocalPort Any `
    -RemotePort 443 `
    -Action Allow
```

---

## Best Practices

### Security

1. **Use Windows Credential Manager for API keys** instead of storing in configuration
2. **Run under a dedicated service account** with least privilege
3. **Enable HTTPS only** for API endpoints
4. **Restrict file system permissions** to only what the service needs
5. **Review logs regularly** for security events

### Reliability

1. **Test configuration** with `-ValidateOnly` before deploying
2. **Monitor Event Log** for service lifecycle events
3. **Set up log rotation** to prevent disk space issues (configured by default)
4. **Configure adequate retry settings** based on API reliability
5. **Test service restart behavior** during maintenance windows

### Performance

1. **Tune `ScanIntervalSeconds`** based on file arrival rate
2. **Adjust `StableAgeSeconds`** for your file copy patterns
3. **Monitor backlog** in processing and inbox folders
4. **Review `MaxFileSizeMB`** to prevent oversized file issues
5. **Use SSD storage** for the database file if possible

### Operations

1. **Document your configuration** (RootPath, API endpoint, etc.)
2. **Create runbooks** for common operational tasks
3. **Set up monitoring alerts** for service failures
4. **Schedule regular reviews** of failed files
5. **Plan for upgrades** by testing in non-production first

---

## Support and Resources

### Log Locations

- **Windows Event Log**: `Application` log, source `FolderToApiService`
- **JSON Logs**: `C:\Logs\FolderToApi\log-*.json`
- **Service Output**: Captured in Event Log and log files

### Database Location

- **Default**: `C:\ProgramData\FolderToApi\foldertoapi.db`
- **Configurable**: Set via `Database.ConnectionString` in appsettings.json

### Useful Commands

```powershell
# Service status
Get-Service FolderToApiService | Format-List *

# Recent events
Get-EventLog -LogName Application -Source FolderToApiService -Newest 20

# Process info
Get-Process -Name "FolderToApi.Service" | Format-List *

# Folder stats
Get-ChildItem "C:\Data\FileDelivery" -Recurse | Group-Object Directory | Select Name, Count

# Database size
Get-Item "C:\ProgramData\FolderToApi\foldertoapi.db" | Format-Table Name, Length, LastWriteTime
```

---

## Version History

- **v1.0** - Initial deployment scripts and documentation

---

For questions or issues, refer to the project repository or contact your system administrator.

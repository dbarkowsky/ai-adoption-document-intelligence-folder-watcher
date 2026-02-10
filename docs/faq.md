# Frequently Asked Questions (FAQ)

Common questions and answers about the Folder-to-API Delivery Service.

## Table of Contents

1. [General Questions](#general-questions)
2. [Installation and Setup](#installation-and-setup)
3. [Configuration](#configuration)
4. [File Processing](#file-processing)
5. [API Integration](#api-integration)
6. [Performance and Scaling](#performance-and-scaling)
7. [Troubleshooting](#troubleshooting)
8. [Security](#security)
9. [Known Issues](#known-issues)

---

## General Questions

### What is the Folder-to-API Delivery Service?

A Windows Service that monitors a folder for new files and reliably delivers them to a remote HTTP API with guaranteed eventual delivery semantics. It uses a store-and-forward architecture with automatic retry logic.

### What are the main use cases?

- Document ingestion to cloud APIs
- File-based integration between on-premises and cloud systems
- Reliable file delivery with retry and error handling
- Automated document processing workflows

### What platforms are supported?

- **Server:** Windows Server 2016 or later (2019/2022 recommended)
- **Client:** .NET 10.0 runtime (or self-contained deployment)
- **API:** Any HTTP/HTTPS REST API

### How does it differ from FileSystemWatcher?

**FileSystemWatcher** (event-driven):
- Reacts immediately to file changes
- Can miss events if buffer overflows
- More complex error handling

**This Service** (periodic scanning):
- Scans at regular intervals
- Never misses files (deterministic)
- Simpler and more predictable
- Adequate latency for most use cases (30-second default interval)

### Is source code available?

Yes, the service is implemented in .NET and follows standard Windows Service architecture. Source code is typically provided with the deployment package for review and auditing.

---

## Installation and Setup

### Do I need .NET installed on the server?

**Self-contained deployment** (default): No, all dependencies are included in the executable.

**Framework-dependent deployment**: Yes, requires .NET 10.0 Runtime.

### Can I run multiple instances?

Yes, you can run multiple service instances with different configurations. Each instance requires:
- Unique service name
- Unique installation directory
- Unique RootPath, database, and log directories

See [Advanced Configuration](../deployment/DEPLOYMENT.md#multiple-instances) for details.

### Does the service require Administrator privileges?

- **Installation:** Yes, requires Administrator to create the Windows Service
- **Runtime:** No, the service runs under a configured service account (default: LocalSystem)

### Can I run this on Windows 10/11?

While designed for Windows Server, it can run on Windows 10/11 for development or testing purposes. Production deployments should use Windows Server for stability and support.

### How do I upgrade to a new version?

1. Stop the service: `Stop-Service FolderToApiService`
2. Backup configuration: Copy `appsettings.json`
3. Replace executable and DLLs
4. Restore configuration (check for new settings)
5. Start the service: `Start-Service FolderToApiService`

See [Operations Guide](operations.md) for detailed procedures.

---

## Configuration

### Where are configuration files stored?

| File | Location |
|------|----------|
| Main config | `C:\Program Files\FolderToApi\appsettings.json` |
| Production config | `C:\Program Files\FolderToApi\appsettings.Production.json` |
| Development config | `C:\Program Files\FolderToApi\appsettings.Development.json` |

### How do I change the API endpoint?

Edit `appsettings.json`:

```json
{
  "ApiClient": {
    "EndpointUrl": "https://new-api.example.com/upload"
  }
}
```

Then restart the service: `Restart-Service FolderToApiService`

### Can I use environment variables for configuration?

Yes, .NET configuration supports environment variable overrides using double underscore notation:

```powershell
# Set environment variable
[System.Environment]::SetEnvironmentVariable("ApiClient__EndpointUrl", "https://api.example.com/upload", "Machine")

# Restart service
Restart-Service FolderToApiService
```

### How do I increase the scan frequency?

Edit `appsettings.json`:

```json
{
  "FolderWatcher": {
    "ScanIntervalSeconds": 15  // Scan every 15 seconds instead of 30
  }
}
```

Valid range: 10-120 seconds. Lower values = higher CPU usage.

### Can I process files from multiple folders?

Not in a single instance. You can:
1. **Recommended:** Run multiple service instances, each monitoring a different folder
2. **Alternative:** Use subfolder structure and enable `RecursiveScan: true`

---

## File Processing

### What file types are supported?

Configurable via `AllowedExtensions`. Default: `.pdf`, `.docx`, `.doc`, `.tif`, `.tiff`, `.jpg`, `.jpeg`, `.png`

To allow all files, use: `"AllowedExtensions": [""]` (empty string matches all)

### Why aren't my files being processed?

Common reasons:
1. **File still locked** (being written): Wait for file to be fully copied
2. **Wrong extension**: Check `AllowedExtensions` in configuration
3. **File too large**: Check `MaxFileSizeMB` in configuration
4. **Service not running**: Verify with `Get-Service FolderToApiService`
5. **Permissions issue**: Service account can't read inbox folder

See [Troubleshooting Guide](troubleshooting.md#files-not-being-processed) for detailed diagnosis.

### How does the service know when a file is complete?

Two-stage check:
1. **Stability check:** File modification time hasn't changed for `StableAgeSeconds` (default: 10 seconds)
2. **Exclusive open:** Service can open file with `FileShare.None` (not locked by writer)

Both conditions must be met before processing.

### What happens if a file is deleted from inbox before processing?

The service logs a warning and moves on. No error occurs.

### Can I reprocess a file that was already sent?

Yes, move the file from `sent/` back to `inbox/`. The service will treat it as a new file and create a new job.

### What happens to files in the processing folder?

Files remain in `processing/<jobId>/` until:
- Successfully delivered → moved to `sent/`
- Permanently failed → moved to `failed/`
- Service restart → processing resumes from database state

Never manually delete files from the processing folder while the service is running.

---

## API Integration

### What HTTP methods are supported?

Only **POST** is currently supported. The service sends files as `multipart/form-data` POST requests.

### Can I use OAuth authentication?

Not in v1. Current authentication method: API key in header.

**Future enhancement:** OAuth 2.0 client credentials flow is planned.

### Can I use client certificates (mTLS)?

Not in v1. **Future enhancement:** mTLS support is planned.

### How do I handle duplicate uploads?

The service may send the same file multiple times in rare cases (e.g., service restart during upload). Your API should handle duplicates using:
- **Content-based deduplication:** Hash the file contents
- **Request tracking:** Track recent successful uploads by filename/size
- **Idempotency keys (future):** The service will support idempotency keys in a future release

See [API Integration Guide](api-integration.md#idempotency-expectations) for examples.

### What if my API returns 429 (rate limit)?

The service honors `Retry-After` headers and uses exponential backoff. Files will be retried automatically after the specified delay.

### Can I customize the HTTP headers?

Currently, only the API key header name is configurable (`ApiKeyHeaderName`). Other headers are fixed.

**Future enhancement:** Custom headers may be supported in a future release.

---

## Performance and Scaling

### What throughput can the service handle?

**Tested capacity:**
- 500 files/hour sustained throughput
- Up to 50 MB per file (default configuration)
- 5 concurrent uploads (default)

Actual throughput depends on:
- File sizes
- API response time
- Network bandwidth
- Scan interval configuration

### How do I increase throughput?

1. **Reduce scan interval:** Lower `ScanIntervalSeconds` (e.g., 15 instead of 30)
2. **Increase concurrency:** (Requires code modification to change `MaxConcurrentUploads`)
3. **Optimize API performance:** Reduce API latency and increase capacity

### What are the resource requirements?

**Minimal configuration:**
- CPU: 1 vCPU
- RAM: 512 MB
- Disk: 10 GB (for database, logs, and temp file storage)

**Recommended production:**
- CPU: 2 vCPU
- RAM: 2 GB
- Disk: 50 GB (depends on file retention policies)
- Network: 10 Mbps+ for file uploads

### Can I run this in a high-availability configuration?

Not in v1 (single instance only).

**Future enhancement:** Multi-instance HA with shared database (SQL Server/PostgreSQL) is planned.

### How much disk space do I need?

Calculate based on:
```
Peak storage = (Average file size) × (Files/hour) × (Max retention hours + processing time)
```

**Example:**
- 10 MB average file size
- 100 files/hour
- 30-day sent retention = 720 hours
- Processing time ~1 hour

```
Peak = 10 MB × 100 × 721 = ~703 GB
```

**Recommendation:** Plan for 2-3x peak storage to account for growth and failed files.

---

## Troubleshooting

### Service won't start

**Most common causes:**
1. Configuration file error (invalid JSON)
2. Missing RootPath or EndpointUrl in config
3. Permission denied on folders or database
4. Database locked by another process

**Solution:** Check Windows Event Log:
```powershell
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

See [Troubleshooting Guide](troubleshooting.md#service-wont-start) for detailed steps.

### High CPU usage

**Causes:**
- Scan interval too low
- Large folder structure with RecursiveScan enabled
- Inefficient file filtering

**Solution:**
- Increase `ScanIntervalSeconds` (e.g., 60 instead of 30)
- Disable `RecursiveScan` if not needed
- Reduce number of files in inbox

### High memory usage

**Causes:**
- Large files being processed
- Memory leak (rare)

**Solution:**
- Reduce `MaxFileSizeMB` to prevent loading large files
- Restart service during maintenance window if memory continues to grow

### Database growing too large

**Causes:**
- Old job records not cleaned up

**Solution:**
- Manually purge old records:
  ```powershell
  sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "DELETE FROM jobs WHERE state = 'Sent' AND sent_at_utc < datetime('now', '-90 days');"
  sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "VACUUM;"
  ```

**Future enhancement:** Automatic database cleanup is planned.

---

## Security

### How is the API key stored?

**Recommended:** Windows Credential Manager (encrypted by Windows)

**Alternative:** Configuration file (not recommended for production)

See [Security Guide](security.md#api-key-storage) for setup instructions.

### Is communication encrypted?

Yes, the service enforces HTTPS for API communication. TLS 1.2+ is used.

### What service account should I use?

**Development:** LocalSystem (simple, local access only)

**Production (local paths):** LocalSystem or dedicated account

**Production (UNC paths):** NetworkService or domain service account

**Enterprise:** Group Managed Service Account (gMSA)

See [Security Guide](security.md#service-account-configuration) for details.

### Are logs secure?

Logs automatically redact sensitive data (API keys, passwords). However, always review logs before sharing for troubleshooting.

---

## Known Issues

### Issue: Files on SMB shares sometimes not detected

**Symptom:** Files copied to inbox over SMB may not be detected for several scans.

**Cause:** File metadata (LastWriteTime) may not be immediately updated due to SMB caching.

**Workaround:** Increase `StableAgeSeconds` to 15-20 seconds for SMB shares.

**Status:** Working as designed (SMB caching behavior).

---

### Issue: Service fails to start after server reboot

**Symptom:** Service shows "Stopped" after server restart, but manual start succeeds.

**Cause:** Dependency (network, file share) not available at service startup time.

**Workaround:** Configure service to delay start:
```powershell
sc config FolderToApiService start= delayed-auto
```

**Status:** Working as designed (Windows Service startup timing).

---

### Issue: Database corruption after unexpected shutdown

**Symptom:** "database disk image is malformed" error in logs.

**Cause:** Unexpected power loss or forced shutdown during database write.

**Workaround:** Restore from backup or delete database (will be recreated, job history lost).

**Prevention:**
- Enable WAL mode (enabled by default)
- Use UPS for server
- Ensure proper shutdown procedures

**Status:** Mitigated by WAL mode, but not 100% preventable.

---

### Issue: Cross-volume file moves fail

**Symptom:** "Cannot create a file when that file already exists" error when inbox and processing are on different drives.

**Cause:** Windows cannot atomically move files across volumes.

**Solution:** Service automatically falls back to copy-verify-delete for cross-volume moves.

**Status:** Working as designed, handled automatically.

---

### Issue: Windows Credential Manager credentials not found

**Symptom:** "API key not found in Credential Manager" error.

**Cause:** Credentials stored under wrong user context.

**Solution:** Ensure credentials are stored under the service account context:
```powershell
# For LocalSystem, use psexec
psexec -i -s cmd.exe
cmdkey /generic:FolderToApiService /user:ApiKey /pass:your-key
```

**Status:** User error, resolved by correct credential storage.

---

## Additional Resources

### Documentation

- [Installation Guide](../deployment/DEPLOYMENT.md)
- [Configuration Reference](configuration.md)
- [Operations Guide](operations.md)
- [Troubleshooting Guide](troubleshooting.md)
- [Security Guide](security.md)
- [Architecture Documentation](architecture.md)
- [API Integration Guide](api-integration.md)

### Support

For issues not covered in this FAQ:
1. Check [Troubleshooting Guide](troubleshooting.md)
2. Review Windows Event Log and JSON logs
3. Consult system administrator or support team

---

## Contributing to FAQ

Have a question that's not answered here? Contact your system administrator to add it to this document.

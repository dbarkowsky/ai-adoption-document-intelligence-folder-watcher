# Folder-to-API Delivery Service

A robust Windows Service that reliably delivers files from a monitored folder to a remote HTTP API with guaranteed eventual delivery semantics.

## Overview

The Folder-to-API Delivery Service is a production-ready .NET Worker Service designed to run on Windows Server. It continuously monitors a designated inbox folder, validates file completeness, and delivers files to a remote API with automatic retry logic and comprehensive error handling.

### Key Features

- **Guaranteed Delivery**: Store-and-forward architecture ensures files are eventually delivered or moved to dead-letter
- **Intelligent File Handling**: Exclusive-open checks prevent processing of locked or incomplete files
- **Robust Retry Logic**: Exponential backoff with jitter for transient failures
- **Durable State Tracking**: SQLite database maintains job state across service restarts
- **Comprehensive Logging**: Structured JSON logs with sensitive data redaction
- **Production Ready**: Windows Service integration with graceful shutdown and recovery options
- **Flexible Deployment**: Supports both local NTFS and UNC network paths

### Architecture Highlights

```
inbox/ → processing/ → sent/
                   ↓
                failed/
                   ↓
              SQLite DB
```

Files flow through a deterministic state machine with full transactional guarantees.

## Quick Start

### Prerequisites

- Windows Server 2016+ (2019/2022 recommended)
- .NET 10.0 Runtime (or use self-contained deployment)
- Administrator access for installation
- Network connectivity to target API endpoint

### Installation (5 Minutes)

1. **Download or Build** the service
   ```powershell
   # From the repository root
   cd deployment
   .\Publish-Service.ps1
   ```

2. **Configure** the service
   Edit `publish\appsettings.json`:
   ```json
   {
     "FolderWatcher": {
       "RootPath": "C:\\Data\\FileDelivery"
     },
     "ApiClient": {
       "EndpointUrl": "https://api.example.com/upload",
       "ApiKey": "your-api-key-here"
     }
   }
   ```

3. **Install** as Windows Service
   ```powershell
   .\Install-Service.ps1 -SourcePath ".\publish"
   ```

4. **Verify** installation
   ```powershell
   Get-Service FolderToApiService
   Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
   ```

5. **Start using** the service
   - Drop files in `C:\Data\FileDelivery\inbox\`
   - Files are automatically picked up, delivered, and moved to `sent\`

## Documentation

### For System Administrators

- **[Installation Guide](deployment/DEPLOYMENT.md)** - Complete deployment instructions
- **[Configuration Reference](docs/configuration.md)** - All configuration options explained
- **[Operations Guide](docs/operations.md)** - Day-to-day operational procedures
- **[Troubleshooting Guide](docs/troubleshooting.md)** - Common issues and solutions
- **[Security Guide](docs/security.md)** - Security hardening and permissions

### For Developers and Architects

- **[Architecture Documentation](docs/architecture.md)** - System design and component interactions
- **[API Integration Guide](docs/api-integration.md)** - How the service interacts with remote APIs
- **[Requirements Document](REQUIREMENTS.md)** - Complete technical requirements

### Reference

- **[FAQ](docs/faq.md)** - Frequently asked questions
- **[User Stories](user_stories/README.md)** - Implementation tracking

## How It Works

### File Lifecycle

1. **Discovery**: Service scans inbox folder every N seconds (configurable)
2. **Validation**: Checks file is complete using exclusive-open technique
3. **Claiming**: Moves file to processing folder and records job in database
4. **Delivery**: Uploads file to remote API with authentication
5. **Success**: Moves file to sent folder and marks job complete
6. **Failure**: Retries with exponential backoff or moves to failed folder

### Folder Structure

After installation, the service creates this folder structure:

```
<RootPath>/
├── inbox/          ← Drop files here
├── processing/     ← Service working directory (do not modify)
├── sent/           ← Successfully delivered files (auto-cleanup after 30 days)
└── failed/         ← Permanently failed deliveries (requires manual review)
```

### State Persistence

All job state is tracked in a local SQLite database:
- Survives service restarts and server reboots
- Enables deterministic recovery
- Provides forensics for troubleshooting

## Configuration Overview

Key configuration sections:

| Section | Purpose |
|---------|---------|
| `FolderWatcher` | Root path, scan interval, file filters |
| `ApiClient` | API endpoint, authentication, timeouts |
| `RetryPolicy` | Max attempts, backoff schedule |
| `ArchiveRetention` | Auto-cleanup for sent/failed folders |
| `Database` | SQLite connection string |
| `Logging` | Log levels, destinations, rotation |

See [Configuration Reference](docs/configuration.md) for complete details.

## Monitoring and Health

### Log Locations

- **Windows Event Log**: Application log, source `FolderToApiService`
- **JSON Log Files**: `C:\Logs\FolderToApi\log-YYYYMMDD.json`
- **SQLite Database**: `C:\ProgramData\FolderToApi\foldertoapi.db`

### Quick Health Check

```powershell
# Service status
Get-Service FolderToApiService

# Recent log entries
Get-EventLog -LogName Application -Source FolderToApiService -Newest 10

# Pending files
Get-ChildItem "C:\Data\FileDelivery\inbox"
Get-ChildItem "C:\Data\FileDelivery\processing"

# Failed files (require attention)
Get-ChildItem "C:\Data\FileDelivery\failed"
```

## Common Operations

### Start/Stop/Restart Service

```powershell
Start-Service FolderToApiService
Stop-Service FolderToApiService
Restart-Service FolderToApiService
```

### Update Configuration

1. Edit `C:\Program Files\FolderToApi\appsettings.json`
2. Restart service: `Restart-Service FolderToApiService`

### Review Failed Deliveries

```powershell
# List failed files
Get-ChildItem "C:\Data\FileDelivery\failed"

# Check database for error details
sqlite3 "C:\ProgramData\FolderToApi\foldertoapi.db" "SELECT job_id, source_path, last_error FROM jobs WHERE state='Failed' ORDER BY last_attempt DESC LIMIT 10;"
```

## Troubleshooting Quick Reference

| Symptom | Check | Solution |
|---------|-------|----------|
| Service won't start | Event Log | Validate configuration, check permissions |
| Files not processing | Inbox folder | Verify file extensions, check for locks |
| API delivery fails | Network connectivity | Check endpoint URL, verify API key |
| High CPU usage | Backlog size | Increase scan interval, review file filters |

See [Troubleshooting Guide](docs/troubleshooting.md) for detailed solutions.

## Security Considerations

- **API Key Storage**: Use Windows Credential Manager instead of config files
- **Service Account**: Run as dedicated account with least privilege
- **Network Security**: Enforce HTTPS-only for API endpoints
- **File Permissions**: Restrict access to processing and database folders
- **Log Redaction**: Sensitive data automatically redacted from logs

See [Security Guide](docs/security.md) for hardening procedures.

## Performance Characteristics

**Tested Capacity:**
- 500 files/hour sustained throughput
- Up to 50 MB per file (configurable)
- Minimal CPU/memory footprint
- Handles burst ingestion gracefully

**Scalability:**
- Single instance per root folder (v1)
- Multiple instances supported with separate configurations
- Future enhancement: multi-instance HA with shared database

## Technology Stack

- **.NET 10.0** - Modern, performant framework
- **Worker Service** - Long-running background service template
- **SQLite** - Embedded, zero-config database
- **Serilog** - Structured logging with multiple sinks
- **IHttpClientFactory** - Proper HTTP client lifecycle management

## Support and Contributing

### Getting Help

1. Check [FAQ](docs/faq.md) for common questions
2. Review [Troubleshooting Guide](docs/troubleshooting.md) for known issues
3. Examine Windows Event Log and JSON logs for errors
4. Consult [Operations Guide](docs/operations.md) for procedures

### Reporting Issues

When reporting issues, include:
- Service version
- Windows Server version
- Configuration (redact sensitive values)
- Recent log entries from Event Log or JSON logs
- Database state for affected jobs

### Development

The project follows a user story-driven development approach. See [user_stories/README.md](user_stories/README.md) for implementation tracking and [REQUIREMENTS.md](REQUIREMENTS.md) for complete technical specifications.

## Version History

- **v1.0** - Initial release
  - Core scan/deliver functionality
  - SQLite persistence
  - Retry with exponential backoff
  - Windows Service deployment
  - Comprehensive logging
  - Auto-cleanup for archives

## License

[Your License Here]

## Contact

[Your Contact Information Here]

---

**Ready to get started?** Follow the [Installation Guide](deployment/DEPLOYMENT.md) for detailed deployment instructions.

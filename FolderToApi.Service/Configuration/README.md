# Configuration Guide

## Overview
This service uses strongly-typed configuration with validation. All configuration is loaded from `appsettings.json` and environment-specific override files.

## Configuration Sections

### FolderWatcher
- **RootPath** (required): Base path for all folders (inbox, processing, sent, failed)
- **ScanIntervalSeconds** (10-120, default: 30): How often to scan for new files
- **StableAgeSeconds** (0-300, default: 10): Minimum age before attempting to claim a file
- **AllowedExtensions** (required): Array of file extensions to process (e.g., `[".pdf", ".docx"]`)
- **MaxFileSizeMB** (1-1024, default: 50): Maximum file size to process
- **RecursiveScan** (default: false): Whether to scan subfolders

### ApiClient
- **EndpointUrl** (required): HTTPS URL of the remote API endpoint
- **ApiKeySource** (default: "Configuration"): Source for API key - "Configuration", "CredentialManager", or "DPAPI"
- **ApiKey**: API key value (plain text if ApiKeySource is "Configuration", encrypted Base64 if "DPAPI")
- **CredentialName**: Name in Windows Credential Manager (if ApiKeySource is "CredentialManager")
- **ApiKeyHeaderName** (default: "X-API-Key"): HTTP header name for the API key
- **ConnectTimeoutSeconds** (1-300, default: 10): Connection timeout
- **RequestTimeoutSeconds** (1-600, default: 60): Overall request timeout

### RetryPolicy
- **MaxAttempts** (1-100, default: 10): Maximum retry attempts before dead-lettering
- **MaxJobAgeDays** (1-365, default: 7): Maximum job age before dead-lettering
- **InitialDelaySeconds** (1-3600, default: 10): Initial retry delay
- **MaxDelaySeconds** (60-86400, default: 3600): Maximum retry delay (backoff cap)
- **BackoffMultiplier** (1.1-10.0, default: 2.0): Exponential backoff multiplier
- **JitterFactor** (0.0-0.5, default: 0.1): Random jitter to prevent thundering herd

### ArchiveRetention
- **SentRetentionDays** (1-365, default: 30): How long to keep successfully sent files
- **FailedRetentionDays** (1-730, default: 90): How long to keep failed files
- **EnableAutoCleanup** (default: true): Whether to automatically clean up old files
- **CleanupIntervalHours** (1-168, default: 24): How often to run cleanup

### Database
- **ConnectionString** (required, default: "Data Source=foldertoapi.db"): SQLite connection string
- **EnableWalMode** (default: true): Enable SQLite WAL mode for better concurrency

## Secure API Key Storage

### Option 1: Plain Text (Development Only)
```json
"ApiClient": {
  "ApiKeySource": "Configuration",
  "ApiKey": "your-api-key-here"
}
```

### Option 2: Windows DPAPI (Recommended for Production)
1. On the target Windows Server, run the encryption tool:
   ```powershell
   .\Tools\EncryptApiKey.ps1 -ApiKey "your-api-key-here"
   ```

2. Copy the encrypted value to appsettings.Production.json:
   ```json
   "ApiClient": {
     "ApiKeySource": "DPAPI",
     "ApiKey": "encrypted-base64-value-here"
   }
   ```

### Option 3: Windows Credential Manager (Future)
```json
"ApiClient": {
  "ApiKeySource": "CredentialManager",
  "CredentialName": "FolderToApiService_ApiKey"
}
```

## Environment-Specific Configuration

- **appsettings.json**: Base configuration with defaults
- **appsettings.Development.json**: Development overrides (loaded when DOTNET_ENVIRONMENT=Development)
- **appsettings.Production.json**: Production overrides (loaded when DOTNET_ENVIRONMENT=Production)

## Validation

All configuration is validated on service startup using Data Annotations. If validation fails, the service will fail to start with clear error messages indicating which configuration values are invalid.

## Example Production Configuration

```json
{
  "FolderWatcher": {
    "RootPath": "\\\\fileserver\\share\\FolderWatcher",
    "ScanIntervalSeconds": 30,
    "AllowedExtensions": [".pdf", ".docx", ".tif"]
  },
  "ApiClient": {
    "EndpointUrl": "https://api.production.com/v1/upload",
    "ApiKeySource": "DPAPI",
    "ApiKey": "AQAAANCMnd8BFdERjHoAw...encrypted..."
  },
  "Database": {
    "ConnectionString": "Data Source=C:\\ProgramData\\FolderToApiService\\foldertoapi.db"
  }
}
```

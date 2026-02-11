# Deployment Scripts

This directory contains PowerShell scripts for deploying the FolderToApiService as a Windows Service.

## Scripts

### Publish-Service.ps1
Builds and publishes the application as a single-file executable.

**Usage:**
```powershell
.\Publish-Service.ps1
```

**Options:**
- `-Configuration <Debug|Release>` - Build configuration (default: Release)
- `-Runtime <win-x64|win-x86|win-arm64>` - Target runtime (default: win-x64)
- `-OutputPath <path>` - Output directory (default: .\publish)
- `-IncludeSymbols` - Include debug symbols
- `-NoSelfContained` - Framework-dependent publish

**Output:** Creates `.\publish\` folder with executable and configuration files.

---

### Install-Service.ps1
Installs the FolderToApiService as a Windows Service.

**Usage:**
```powershell
.\Install-Service.ps1 -SourcePath ".\publish"
```

**Options:**
- `-InstallPath <path>` - Installation directory (default: C:\Program Files\FolderToApi)
- `-ServiceName <name>` - Service name (default: FolderToApiService)
- `-ServiceAccount <LocalSystem|NetworkService|LocalService|Custom>` - Account to run service (default: LocalSystem)
- `-CustomServiceAccount <account>` - Domain account (e.g., DOMAIN\ServiceAccount)
- `-ServicePassword <SecureString>` - Password for custom account
- `-ValidateOnly` - Validate configuration without installing

**Prerequisites:**
- Must run as Administrator
- Source files must exist (run Publish-Service.ps1 first)
- Configuration must be valid

---

### Uninstall-Service.ps1
Uninstalls the FolderToApiService Windows Service.

**Usage:**
```powershell
.\Uninstall-Service.ps1
```

**Options:**
- `-ServiceName <name>` - Service name to uninstall (default: FolderToApiService)
- `-RemoveFiles` - Remove installation files
- `-RemoveData` - Remove database and log files (⚠️ use with caution)
- `-Force` - Skip confirmation prompts

---

## Quick Start

### 1. Publish the Application

**On Windows (PowerShell):**
```powershell
# From the repository root
cd deployment
.\Publish-Service.ps1
```

**On Linux or macOS (no PowerShell):** Use the shell script instead:
```bash
cd deployment
./publish-service.sh
```
Or from the repo root: `./deployment/publish-service.sh`

### 2. Copy to Target Server

Copy the `.\publish\` folder to your Windows Server.

### 3. Configure the Application

Edit `publish\appsettings.json`:
- Set `FolderWatcher.RootPath` to your folder path
- Set `ApiClient.EndpointUrl` to your API endpoint
- Set `ApiClient.ApiKey` (or configure Credential Manager)

### 4. Install as Windows Service

On the target server (run as Administrator):

```powershell
cd C:\Temp\publish
.\Install-Service.ps1 -SourcePath "C:\Temp\publish"
```

### 5. Verify Installation

```powershell
Get-Service FolderToApiService
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

---

## Documentation

See [DEPLOYMENT.md](DEPLOYMENT.md) for comprehensive deployment guide including:
- Prerequisites
- Configuration options
- Service management
- Troubleshooting
- Advanced scenarios

---

## Examples

### Install with Network Service Account

```powershell
.\Install-Service.ps1 -ServiceAccount NetworkService
```

### Install with Domain Service Account

```powershell
$password = ConvertTo-SecureString "P@ssw0rd" -AsPlainText -Force
.\Install-Service.ps1 `
    -ServiceAccount Custom `
    -CustomServiceAccount "DOMAIN\SvcFolderToApi" `
    -ServicePassword $password
```

### Validate Configuration Only

```powershell
.\Install-Service.ps1 -ValidateOnly
```

### Complete Uninstall

```powershell
.\Uninstall-Service.ps1 -RemoveFiles -Force
```

---

## Troubleshooting

### "This script must be run as Administrator"
Right-click PowerShell → Run as Administrator

### "Executable not found"
Run `Publish-Service.ps1` first to create the executable

### "Configuration validation failed"
- Check that `RootPath` is set in appsettings.json
- Check that `EndpointUrl` is set in appsettings.json
- Validate JSON syntax is correct

### Service won't start
```powershell
# Check Event Log for errors
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5

# Validate configuration
.\Install-Service.ps1 -ValidateOnly
```

---

## Requirements

- **Development Machine:** .NET 10.0 SDK
- **Target Server:** Windows Server 2016+ with PowerShell 5.1+
- **Permissions:** Administrator rights for installation
- **Network:** Connectivity to target API endpoint

---

## Support

For detailed documentation, see [DEPLOYMENT.md](DEPLOYMENT.md).

For issues or questions, refer to the project repository.

# Security and Permissions Guide

Comprehensive security guidance for the Folder-to-API Delivery Service.

## Table of Contents

1. [Security Overview](#security-overview)
2. [API Key Storage](#api-key-storage)
3. [Service Account Configuration](#service-account-configuration)
4. [File System Permissions](#file-system-permissions)
5. [Network Security](#network-security)
6. [Log Redaction](#log-redaction)
7. [Security Hardening Checklist](#security-hardening-checklist)
8. [Compliance Considerations](#compliance-considerations)

---

## Security Overview

### Threat Model

**Assets Protected:**
- API keys (authentication credentials)
- Files in transit (inbox → processing → sent/failed)
- Job metadata (database)
- Log files (may contain sensitive information)

**Threats Mitigated:**
- Unauthorized access to API endpoint
- Exposure of API keys in configuration or logs
- Unauthorized file access or modification
- Man-in-the-middle attacks on API communication
- Privilege escalation via service account

### Security Principles

1. **Least Privilege:** Service runs with minimum required permissions
2. **Defense in Depth:** Multiple layers of security controls
3. **Secure by Default:** Secure defaults in configuration
4. **Audit Trail:** Comprehensive logging for security events
5. **Encrypted Communication:** HTTPS-only for API communication

---

## API Key Storage

### Option 1: Windows Credential Manager (Recommended)

**Advantages:**
- ✅ Encrypted by Windows (DPAPI)
- ✅ Scoped to service account
- ✅ Not stored in configuration files
- ✅ Survives configuration changes

**Setup:**

#### Step 1: Configure Service to Use Credential Manager

Edit `appsettings.json`:

```json
{
  "ApiClient": {
    "EndpointUrl": "https://api.example.com/upload",
    "ApiKeySource": "WindowsCredentialManager",
    "ApiKey": ""  // Leave empty when using Credential Manager
  }
}
```

#### Step 2: Store API Key in Credential Manager

**As the service account** (important!):

```powershell
# Method 1: Using cmdkey (run as service account)
# Switch to service account context first
runas /user:DOMAIN\ServiceAccount "cmdkey /generic:FolderToApiService /user:ApiKey /pass:your-actual-api-key"

# Method 2: Using PowerShell (run as service account)
$credential = New-Object -TypeName PSCredential -ArgumentList "ApiKey", (ConvertTo-SecureString "your-actual-api-key" -AsPlainText -Force)
# Save to Credential Manager
# (Note: Requires running as the service account)
```

**For LocalSystem account:**

```powershell
# Install PSExec from Sysinternals
# https://docs.microsoft.com/en-us/sysinternals/downloads/psexec

# Run as LocalSystem to add credential
psexec -i -s cmd.exe
cmdkey /generic:FolderToApiService /user:ApiKey /pass:your-actual-api-key
```

#### Step 3: Verify Credential Storage

```powershell
# List stored credentials
cmdkey /list | Select-String "FolderToApiService"
```

#### Step 4: Test Service Startup

```powershell
Restart-Service FolderToApiService
Get-EventLog -LogName Application -Source FolderToApiService -Newest 5
```

### Option 2: Configuration File (Development Only)

**⚠️ Warning:** Not recommended for production due to security risks.

**Advantages:**
- Simple to set up
- Suitable for development/testing

**Disadvantages:**
- ❌ API key stored in plaintext
- ❌ Visible to anyone with file read access
- ❌ May be accidentally committed to source control

**Setup:**

```json
{
  "ApiClient": {
    "ApiKeySource": "Configuration",
    "ApiKey": "your-api-key-here"
  }
}
```

**Mitigations if using Configuration:**

1. **Restrict file permissions:**
   ```powershell
   # Remove inherited permissions
   icacls "C:\Program Files\FolderToApi\appsettings.json" /inheritance:r

   # Grant access only to Administrators and service account
   icacls "C:\Program Files\FolderToApi\appsettings.json" /grant "BUILTIN\Administrators:(R)"
   icacls "C:\Program Files\FolderToApi\appsettings.json" /grant "NT AUTHORITY\SYSTEM:(R)"
   ```

2. **Use environment-specific configuration:**
   Store sensitive values only in `appsettings.Production.json` (not in source control)

3. **Enable audit logging:**
   ```powershell
   # Enable file access auditing
   auditpol /set /subcategory:"File System" /success:enable /failure:enable
   ```

### Option 3: Azure Key Vault / AWS Secrets Manager (Future)

Planned enhancement for cloud-native deployments.

---

## Service Account Configuration

### Option 1: LocalSystem (Default, Simple)

**Advantages:**
- ✅ No password management required
- ✅ High privileges (can access most local resources)

**Disadvantages:**
- ❌ Cannot access network resources (UNC paths) without special configuration
- ❌ Runs with high privileges (not least privilege)

**Recommended for:** Local-only deployments where RootPath is on local disk.

**Installation:**

```powershell
# Service defaults to LocalSystem
.\deployment\Install-Service.ps1 -SourcePath ".\publish"
```

### Option 2: Network Service (Network Access)

**Advantages:**
- ✅ Can access network resources
- ✅ Lower privileges than LocalSystem
- ✅ No password management

**Disadvantages:**
- ❌ May require explicit permissions on network shares

**Recommended for:** UNC path deployments in domain environments.

**Installation:**

```powershell
.\deployment\Install-Service.ps1 `
    -SourcePath ".\publish" `
    -ServiceAccount NetworkService
```

**Grant network share permissions:**

```powershell
# Grant permissions to DOMAIN\MACHINE$ account
icacls "\\fileserver\share\FileDelivery" /grant "DOMAIN\SERVER1$:(OI)(CI)M"
```

### Option 3: Custom Domain Account (Least Privilege)

**Advantages:**
- ✅ Least privilege principle
- ✅ Can access network resources
- ✅ Explicit permission control
- ✅ Audit trail by account

**Disadvantages:**
- ❌ Requires password management
- ❌ Password rotation complexity

**Recommended for:** Production deployments requiring strict access control.

**Setup:**

#### Step 1: Create Service Account

```powershell
# Create AD service account (run on Domain Controller or with AD tools)
New-ADUser -Name "svc-foldertoapi" `
    -SamAccountName "svc-foldertoapi" `
    -UserPrincipalName "svc-foldertoapi@domain.com" `
    -AccountPassword (ConvertTo-SecureString "ComplexPassword123!" -AsPlainText -Force) `
    -Enabled $true `
    -PasswordNeverExpires $true `
    -Description "Service account for Folder-to-API Delivery Service"
```

#### Step 2: Install Service with Custom Account

```powershell
$password = ConvertTo-SecureString "ComplexPassword123!" -AsPlainText -Force

.\deployment\Install-Service.ps1 `
    -SourcePath ".\publish" `
    -ServiceAccount Custom `
    -CustomServiceAccount "DOMAIN\svc-foldertoapi" `
    -ServicePassword $password
```

#### Step 3: Grant Required Permissions

See [File System Permissions](#file-system-permissions) section below.

### Managed Service Accounts (gMSA)

**Recommended for:** Enterprise deployments with Active Directory.

**Advantages:**
- ✅ Automatic password management
- ✅ No password storage or rotation required
- ✅ Least privilege

**Setup (requires AD admin):**

```powershell
# Create gMSA (on Domain Controller)
New-ADServiceAccount -Name "svc-foldertoapi-gmsa" `
    -DNSHostName "svc-foldertoapi-gmsa.domain.com" `
    -PrincipalsAllowedToRetrieveManagedPassword "SERVER1$"

# Install on target server
Install-ADServiceAccount -Identity "svc-foldertoapi-gmsa"

# Install service with gMSA
.\deployment\Install-Service.ps1 `
    -SourcePath ".\publish" `
    -ServiceAccount Custom `
    -CustomServiceAccount "DOMAIN\svc-foldertoapi-gmsa$"
    # No password required for gMSA
```

---

## File System Permissions

### Required Permissions

| Path | Required Access | Purpose |
|------|----------------|---------|
| `RootPath\inbox` | Read, Write, Delete | Read incoming files, delete after claim |
| `RootPath\processing` | Full Control | Create job folders, write files |
| `RootPath\sent` | Write, Modify | Move delivered files |
| `RootPath\failed` | Write, Modify | Move failed files |
| Database directory | Read, Write, Modify | Read/write SQLite database |
| Log directory | Write, Modify | Create and write log files |
| Installation directory | Read, Execute | Read service executable and config |

### Granting Permissions

#### PowerShell Script

```powershell
# Configuration
$serviceAccount = "DOMAIN\svc-foldertoapi"  # Or "NT AUTHORITY\SYSTEM"
$rootPath = "C:\Data\FileDelivery"
$dbPath = "C:\ProgramData\FolderToApi"
$logPath = "C:\Logs\FolderToApi"
$installPath = "C:\Program Files\FolderToApi"

# Function to grant permissions
function Grant-ServicePermissions {
    param($Path, $Account, $Permissions)

    Write-Host "Granting $Permissions on $Path to $Account"

    # Remove inherited permissions (optional, for strict control)
    # icacls $Path /inheritance:r

    # Grant permissions
    # (OI) = Object Inherit, (CI) = Container Inherit
    # M = Modify, F = Full Control, R = Read, RX = Read & Execute
    icacls $Path /grant "${Account}:${Permissions}" /T

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Success" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Failed" -ForegroundColor Red
    }
}

# Grant permissions
Grant-ServicePermissions -Path $rootPath -Account $serviceAccount -Permissions "(OI)(CI)M"
Grant-ServicePermissions -Path $dbPath -Account $serviceAccount -Permissions "(OI)(CI)M"
Grant-ServicePermissions -Path $logPath -Account $serviceAccount -Permissions "(OI)(CI)M"
Grant-ServicePermissions -Path $installPath -Account $serviceAccount -Permissions "(OI)(CI)RX"

Write-Host "`nPermissions granted successfully" -ForegroundColor Cyan
```

### UNC Path Permissions

For network shares:

```powershell
# 1. Grant NTFS permissions on the file server
$serviceAccount = "DOMAIN\svc-foldertoapi"
$uncPath = "\\fileserver\share\FileDelivery"

icacls $uncPath /grant "${serviceAccount}:(OI)(CI)M" /T

# 2. Grant share permissions
# (Must be done on the file server)
Grant-SmbShareAccess -Name "share" -AccountName $serviceAccount -AccessRight Change -Force

# 3. Test access from service server
Test-Path $uncPath
New-Item -Path "$uncPath\test.txt" -ItemType File -Force
Remove-Item -Path "$uncPath\test.txt" -Force
```

### Permission Verification

```powershell
# Check effective permissions
icacls "C:\Data\FileDelivery"

# Test write access
$testFile = "C:\Data\FileDelivery\inbox\test-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
try {
    "Test" | Out-File $testFile -Force
    Remove-Item $testFile -Force
    Write-Host "Write access verified" -ForegroundColor Green
} catch {
    Write-Host "Write access FAILED: $($_.Exception.Message)" -ForegroundColor Red
}
```

---

## Network Security

### HTTPS Enforcement

**Configuration:**

```json
{
  "ApiClient": {
    "EndpointUrl": "https://api.example.com/upload"  // Always use HTTPS
  }
}
```

**Service Behavior:**
- The service accepts HTTPS URLs only (HTTP URLs are rejected during config validation)
- TLS certificate validation is enabled by default
- Supports TLS 1.2+ (TLS 1.0/1.1 disabled for security)

### TLS Configuration

#### Enable TLS 1.2 on Windows Server

```powershell
# Registry keys for TLS 1.2 (run as Administrator)
New-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' `
    -Name 'DisabledByDefault' -Value 0 -PropertyType 'DWORD' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client' `
    -Name 'Enabled' -Value 1 -PropertyType 'DWORD' -Force

# Restart required
Restart-Computer
```

#### Disable Older TLS Versions

```powershell
# Disable TLS 1.0
New-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.0\Client' `
    -Name 'Enabled' -Value 0 -PropertyType 'DWORD' -Force

# Disable TLS 1.1
New-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client' -Force
New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client' `
    -Name 'Enabled' -Value 0 -PropertyType 'DWORD' -Force
```

### Firewall Configuration

```powershell
# Allow outbound HTTPS for service executable
New-NetFirewallRule -DisplayName "FolderToApiService Outbound HTTPS" `
    -Direction Outbound `
    -Program "C:\Program Files\FolderToApi\FolderToApi.Service.exe" `
    -Protocol TCP `
    -RemotePort 443 `
    -Action Allow `
    -Profile Any

# Verify rule
Get-NetFirewallRule -DisplayName "FolderToApiService Outbound HTTPS"
```

### Certificate Validation

The service validates server certificates by default. If you need to use self-signed certificates (not recommended for production):

**⚠️ Warning:** Only for testing/development environments.

```csharp
// Custom certificate validation (code modification required, not available via config)
// NOT RECOMMENDED FOR PRODUCTION
ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
```

**Better approach:** Add self-signed certificate to Trusted Root Certification Authorities:

```powershell
# Import certificate
Import-Certificate -FilePath "C:\certs\api-server-cert.cer" -CertStoreLocation Cert:\LocalMachine\Root
```

---

## Log Redaction

### Sensitive Data Redaction

The service automatically redacts sensitive information from logs:

**Redacted Fields:**
- API keys
- Authentication headers
- Passwords
- Access tokens

**Example:**

```json
// Before redaction
{
  "Message": "HTTP request",
  "Headers": {
    "X-API-Key": "sk_live_abc123xyz789..."
  }
}

// After redaction
{
  "Message": "HTTP request",
  "Headers": {
    "X-API-Key": "[REDACTED]"
  }
}
```

### Configuration

Redaction is enabled by default. No configuration required.

**Implementation:** Uses Serilog enricher to scrub sensitive data before writing logs.

### Manual Log Review

When sharing logs for troubleshooting, always review for sensitive data:

```powershell
# Search logs for potential sensitive data
Select-String -Path "C:\Logs\FolderToApi\*.json" -Pattern "api.?key|password|secret|token" -CaseSensitive:$false
```

---

## Security Hardening Checklist

### Installation Security

- [ ] Use Windows Credential Manager for API key storage (not configuration files)
- [ ] Run service with dedicated account (not LocalSystem) for production
- [ ] Apply principle of least privilege to service account
- [ ] Restrict file system permissions to service account only
- [ ] Enable audit logging on sensitive folders

### Network Security

- [ ] Use HTTPS endpoints only (never HTTP in production)
- [ ] Verify TLS 1.2+ is enabled on server
- [ ] Disable TLS 1.0/1.1 on server
- [ ] Configure firewall to allow only necessary outbound connections
- [ ] Validate server certificates (don't skip validation)

### Operational Security

- [ ] Regularly rotate API keys (establish rotation schedule)
- [ ] Review Event Log for suspicious activity
- [ ] Monitor failed login attempts
- [ ] Implement log retention and archival
- [ ] Backup database securely (encrypted)

### Access Control

- [ ] Restrict RDP/remote access to server
- [ ] Implement strong passwords for service accounts
- [ ] Use gMSA for automated password management
- [ ] Review and audit service account permissions quarterly
- [ ] Enable Windows Event Log auditing

### Configuration Security

- [ ] Store production config separately from source control
- [ ] Use environment-specific configuration files
- [ ] Restrict access to appsettings.json files
- [ ] Document secrets locations (Credential Manager, Key Vault, etc.)
- [ ] Review configuration for security misconfigurations

---

## Compliance Considerations

### Data Protection

**GDPR / Privacy:**
- Minimize logging of personally identifiable information (PII)
- Implement data retention policies for logs and archives
- Secure file storage with encryption at rest (BitLocker)

**Example Retention Configuration:**

```json
{
  "ArchiveRetention": {
    "SentRetentionDays": 30,     // Keep sent files for 30 days
    "FailedRetentionDays": 90,   // Keep failed files for 90 days (investigation)
    "EnableAutoCleanup": true
  },
  "Logging": {
    "RetainedFileCountLimit": 30  // Keep 30 days of logs
  }
}
```

### Audit Logging

**Enable Windows Audit Policies:**

```powershell
# Enable file access auditing
auditpol /set /subcategory:"File System" /success:enable /failure:enable

# Enable process creation auditing
auditpol /set /subcategory:"Process Creation" /success:enable

# Enable logon auditing
auditpol /set /subcategory:"Logon" /success:enable /failure:enable
```

**Review audit logs:**

```powershell
# Review security event log
Get-EventLog -LogName Security -After (Get-Date).AddDays(-1) | Where-Object { $_.EventID -in 4663, 4656 }
```

### Encryption

**Encryption at Rest:**
- Enable BitLocker on server drives
- Use encrypted network shares (SMB 3.0+ encryption)

```powershell
# Enable BitLocker on drive
Enable-BitLocker -MountPoint "C:" -EncryptionMethod Aes256 -UsedSpaceOnly

# Enable SMB encryption for share
Set-SmbShare -Name "FileDelivery" -EncryptData $true
```

**Encryption in Transit:**
- HTTPS for API communication (enforced)
- SMB 3.0+ encryption for network shares

---

## Security Incident Response

### Suspected API Key Compromise

1. **Immediate action:**
   ```powershell
   # Stop service
   Stop-Service FolderToApiService
   ```

2. **Rotate API key:**
   - Generate new API key from API provider
   - Update Credential Manager or configuration
   - Revoke old API key

3. **Investigate:**
   - Review Event Log for unauthorized access
   - Review API provider logs for suspicious activity
   - Check file system audit logs

4. **Resume service:**
   ```powershell
   Start-Service FolderToApiService
   ```

### Suspected File System Breach

1. **Assess impact:**
   ```powershell
   # Check for unauthorized file modifications
   Get-ChildItem "C:\Data\FileDelivery" -Recurse | Where-Object { $_.LastWriteTime -gt (Get-Date).AddHours(-1) }
   ```

2. **Review audit logs:**
   ```powershell
   Get-EventLog -LogName Security -EntryType FailureAudit -After (Get-Date).AddDays(-7)
   ```

3. **Restore from backup if needed** (see [Operations Guide](operations.md#backup-and-recovery))

---

## Next Steps

- **[Operations Guide](operations.md)** - Routine operational procedures
- **[Configuration Reference](configuration.md)** - Configure security settings
- **[Troubleshooting Guide](troubleshooting.md)** - Resolve permission issues
- **[Architecture Documentation](architecture.md)** - Understand security architecture

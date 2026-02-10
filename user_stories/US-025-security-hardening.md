# US-025: Security Hardening and Secret Management

**As a** Security Engineer,
**I want to** ensure the service follows security best practices for secret storage, permissions, and API communication,
**So that** sensitive data is protected and the attack surface is minimized.

## Acceptance Criteria
- [ ] **Scenario 1**: Store API Key Securely
    - **Given** the API key is sensitive
    - **When** configuration is set up
    - **Then** the API key is stored using Windows Credential Manager or DPAPI-protected configuration, not in plaintext

- [ ] **Scenario 2**: Redact Secrets from Logs
    - **Given** logs may contain HTTP headers or config values
    - **When** logs are written
    - **Then** API keys, authorization headers, and other sensitive fields are redacted or omitted

- [ ] **Scenario 3**: Enforce HTTPS for API Endpoint
    - **Given** the API endpoint URL is configured
    - **When** the service makes HTTP requests
    - **Then** only HTTPS URLs are allowed (HTTP URLs are rejected with validation error)

- [ ] **Scenario 4**: Validate TLS Certificates
    - **Given** the API uses HTTPS
    - **When** the service connects
    - **Then** TLS certificates are validated (no self-signed or invalid certs accepted unless explicitly configured for dev/test)

- [ ] **Scenario 5**: Run Service with Least-Privilege Account
    - **Given** the service needs to access specific folders and network
    - **When** the service is installed
    - **Then** a dedicated service account is created with minimal permissions (read/write to RootPath, SQLite DB, network access only)

- [ ] **Scenario 6**: Secure SQLite Database File
    - **Given** the SQLite database contains job metadata
    - **When** the database file is created
    - **Then** NTFS permissions are set to allow only the service account (deny read to other users)

- [ ] **Scenario 7**: Validate Input File Paths
    - **Given** file paths are processed from the inbox
    - **When** files are claimed or moved
    - **Then** path traversal attacks are prevented (validate paths stay within configured RootPath)

- [ ] **Scenario 8**: Audit Log Sensitive Operations
    - **Given** security-relevant operations occur (service start/stop, config changes, failed auth)
    - **When** these operations occur
    - **Then** audit events are logged to Windows Event Log for security monitoring

## Priority
- [ ] High (Must Have)
- [x] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- API Key storage options:
  - Windows Credential Manager: use CredentialManager NuGet package
  - DPAPI: use ProtectedData.Protect/Unprotect
  - Environment variables (less preferred)
- Secret redaction in logs:
  - Implement log filter to redact headers: X-API-Key, Authorization
  - Redact config values: ApiKey
- HTTPS enforcement:
  - Validate EndpointUrl starts with "https://"
  - Reject HTTP URLs unless AllowHttp=true (dev/test only)
- TLS validation:
  - Default: validate certificates
  - Optional: AllowInvalidCertificates=true for dev/test (log warning)
- Service account permissions:
  - Read/Write/Modify: <Root>\\inbox, processing, sent, failed
  - Read/Write: SQLite database directory
  - Network: HTTPS to API endpoint
  - Deny: everything else (principle of least privilege)
- SQLite permissions:
  - Set NTFS ACL on database file: service account full control, deny all others
- Path traversal prevention:
  - Validate all file paths with Path.GetFullPath and ensure they start with RootPath
- Audit events:
  - Service start/stop
  - Configuration loaded
  - API authentication failures
  - Dead-letter events

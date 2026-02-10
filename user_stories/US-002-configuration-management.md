# US-002: Configuration Management with Secure Secret Storage

**As a** DevOps Engineer,
**I want to** define and load all service configuration from appsettings.json with secure API key storage,
**So that** I can configure the service for different environments without code changes and protect sensitive credentials.

## Acceptance Criteria
- [ ] **Scenario 1**: Define Configuration Schema
    - **Given** the service needs configuration for paths, scanning, and API settings
    - **When** I create appsettings.json with all required keys
    - **Then** the configuration includes RootPath, EndpointUrl, ApiKey, ScanIntervalSeconds, StableAgeSeconds, AllowedExtensions, MaxFileSizeMB, MaxAttempts, MaxJobAgeDays, retry/backoff parameters, and archive retention settings

- [ ] **Scenario 2**: Bind Configuration to Strongly-Typed Classes
    - **Given** configuration needs to be type-safe and validated
    - **When** I use IOptions<T> pattern with configuration binding
    - **Then** configuration is loaded into strongly-typed POCOs with validation

- [ ] **Scenario 3**: Secure API Key Storage
    - **Given** API keys must not be stored in plaintext in logs or configuration files
    - **When** I configure the API key storage mechanism
    - **Then** API keys are stored using Windows Credential Manager or DPAPI-protected configuration

- [ ] **Scenario 4**: Configuration Validation on Startup
    - **Given** invalid configuration can cause runtime failures
    - **When** the service starts with missing or invalid configuration
    - **Then** the service fails fast with clear error messages indicating which configuration values are invalid

- [ ] **Scenario 5**: Environment-Specific Configuration
    - **Given** different environments need different settings
    - **When** I provide appsettings.Production.json or appsettings.Development.json
    - **Then** the correct environment-specific configuration overrides are applied

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use IOptions<T> pattern for configuration
- Validate configuration on startup (fail fast if invalid)
- Prefer Windows Credential Manager or DPAPI for API key storage
- Configuration should support:
  - ScanIntervalSeconds: default 30, range 10-120
  - StableAgeSeconds: default 5-10 seconds
  - AllowedExtensions: array of strings (e.g., [".pdf", ".docx", ".tif"])
  - MaxFileSizeMB: guardrail for file size
  - MaxAttempts and/or MaxJobAgeDays for dead-letter rules
  - Retry backoff schedule parameters

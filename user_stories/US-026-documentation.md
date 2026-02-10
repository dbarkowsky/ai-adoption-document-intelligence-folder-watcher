# US-026: Comprehensive Documentation and Operations Guide

**As a** System Administrator,
**I want to** have complete documentation for installation, configuration, operation, and troubleshooting,
**So that** I can deploy and maintain the service without developer assistance.

## Acceptance Criteria
- [x] **Scenario 1**: Installation Guide
    - **Given** a new Windows Server deployment
    - **When** I follow the installation guide
    - **Then** step-by-step instructions cover: prerequisites, building/publishing, creating service account, setting permissions, installing service, and validating configuration
    - **Implementation:** See [deployment/DEPLOYMENT.md](../deployment/DEPLOYMENT.md)

- [x] **Scenario 2**: Configuration Reference
    - **Given** I need to configure the service for my environment
    - **When** I consult the configuration reference
    - **Then** all configuration keys are documented with: name, type, default value, valid range, required/optional, description, and examples
    - **Implementation:** See [docs/configuration.md](../docs/configuration.md)

- [x] **Scenario 3**: Operations Guide
    - **Given** the service is running in production
    - **When** I need to perform operational tasks
    - **Then** the guide covers: starting/stopping service, monitoring health, checking logs, reviewing failed jobs, purging archives, and updating configuration
    - **Implementation:** See [docs/operations.md](../docs/operations.md)

- [x] **Scenario 4**: Troubleshooting Guide
    - **Given** the service is experiencing issues
    - **When** I consult the troubleshooting guide
    - **Then** common problems are documented with symptoms, causes, and solutions (e.g., "Files not processing", "API errors", "Service crashes")
    - **Implementation:** See [docs/troubleshooting.md](../docs/troubleshooting.md)

- [x] **Scenario 5**: Architecture and Design Documentation
    - **Given** I need to understand how the service works
    - **When** I read the architecture documentation
    - **Then** diagrams and explanations cover: folder layout, state machine, scan/send loops, retry policy, database schema, and component interactions
    - **Implementation:** See [docs/architecture.md](../docs/architecture.md)

- [x] **Scenario 6**: API Integration Guide
    - **Given** the remote API team needs to understand client behavior
    - **When** they read the integration guide
    - **Then** the guide documents: HTTP request format, authentication, retry behavior, expected response codes, and idempotency expectations
    - **Implementation:** See [docs/api-integration.md](../docs/api-integration.md)

- [x] **Scenario 7**: Security and Permissions Guide
    - **Given** I need to configure service account and permissions
    - **When** I follow the security guide
    - **Then** the guide covers: creating service account, setting NTFS permissions, configuring API key storage, and security best practices
    - **Implementation:** See [docs/security.md](../docs/security.md)

- [x] **Scenario 8**: FAQ and Known Issues
    - **Given** I have questions or encounter known issues
    - **When** I check the FAQ
    - **Then** common questions and known issues are documented with answers and workarounds
    - **Implementation:** See [docs/faq.md](../docs/faq.md)

## Priority
- [ ] High (Must Have)
- [x] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Documentation format: Markdown files in repository
- Documentation structure:
  - README.md: overview and quick start
  - docs/installation.md: installation guide
  - docs/configuration.md: configuration reference
  - docs/operations.md: operations guide
  - docs/troubleshooting.md: troubleshooting guide
  - docs/architecture.md: architecture and design
  - docs/api-integration.md: API integration guide
  - docs/security.md: security and permissions
  - docs/faq.md: FAQ and known issues
- Include diagrams:
  - Folder layout and file lifecycle
  - State machine diagram
  - Component interaction diagram
  - Deployment architecture
- Code samples for:
  - Installation scripts (PowerShell)
  - Configuration examples (appsettings.json)
  - SQL queries for troubleshooting
- Provide runbooks for common tasks:
  - Restart service
  - Review failed jobs
  - Purge old files
  - Update configuration
  - Rotate API key

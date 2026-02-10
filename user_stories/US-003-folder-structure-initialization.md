# US-003: Folder Structure Initialization and Validation

**As a** System Administrator,
**I want to** automatically create and validate the required folder structure on service startup,
**So that** the service has all necessary directories (inbox, processing, sent, failed) and proper permissions.

## Acceptance Criteria
- [ ] **Scenario 1**: Create Required Folders on First Run
    - **Given** the service is starting for the first time
    - **When** the RootPath is configured and the service initializes
    - **Then** the service creates `<Root>\inbox`, `<Root>\processing`, `<Root>\sent`, and `<Root>\failed` directories if they don't exist

- [ ] **Scenario 2**: Validate Folder Permissions
    - **Given** the service account needs read/write access to all folders
    - **When** the service starts
    - **Then** the service verifies it can read and write to inbox, processing, sent, and failed directories and logs a warning or fails if permissions are insufficient

- [ ] **Scenario 3**: Support Both Local and UNC Paths
    - **Given** the RootPath can be a local NTFS path or SMB UNC share
    - **When** the service initializes with either path type
    - **Then** the service successfully creates and validates folders for both local (e.g., C:\Data\FileWatcher) and UNC paths (e.g., \\server\share\FileWatcher)

- [ ] **Scenario 4**: Handle Missing Root Path Gracefully
    - **Given** the configured RootPath doesn't exist or is unreachable
    - **When** the service attempts to initialize
    - **Then** the service logs a clear error message and either retries or fails fast depending on configuration

- [ ] **Scenario 5**: Validate SQLite Database Location
    - **Given** SQLite database must be on local disk (not SMB)
    - **When** the service starts
    - **Then** the service verifies the SQLite database path is on a local volume and creates the database file if it doesn't exist

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Folder layout: `<Root>\inbox`, `<Root>\processing`, `<Root>\sent`, `<Root>\failed`
- Service account needs Read/Write/Modify permissions on all folders
- SQLite database should be stored on local disk (not SMB share) for reliability
- Support both local NTFS paths and UNC paths for RootPath
- Create folders with appropriate error handling for network shares
- Log folder creation and validation results for troubleshooting

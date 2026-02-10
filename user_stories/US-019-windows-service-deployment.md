# US-019: Windows Service Deployment and Installation

**As a** DevOps Engineer,
**I want to** publish and install the application as a Windows Service with automated setup,
**So that** the service runs automatically on server startup and can be managed via Service Control Manager.

## Acceptance Criteria
- [x] **Scenario 1**: Publish as Single-File Executable
    - **Given** the application is ready for deployment
    - **When** I run `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
    - **Then** a single-file executable is created in the publish folder with all dependencies embedded
    - **Implementation**: `deployment/Publish-Service.ps1`

- [x] **Scenario 2**: Create Installation Script
    - **Given** the service needs to be installed on Windows Server
    - **When** I run the installation script (PowerShell or batch)
    - **Then** the script copies the executable to `C:\Program Files\<Company>\FolderToApi\`, creates the service using `sc.exe create`, and configures recovery options
    - **Implementation**: `deployment/Install-Service.ps1`

- [x] **Scenario 3**: Configure Service Account and Permissions
    - **Given** the service needs a dedicated service account
    - **When** the installation script runs
    - **Then** the service is configured to run under a specified service account with proper permissions to RootPath, SQLite database, and network access
    - **Implementation**: `Install-Service.ps1` supports LocalSystem, NetworkService, LocalService, and custom accounts with automatic permission configuration

- [x] **Scenario 4**: Set Service to Auto-Start
    - **Given** the service should start automatically on server boot
    - **When** the service is created
    - **Then** the start type is set to Automatic using `sc.exe config <service> start=auto`
    - **Implementation**: Service configured with delayed auto-start in `Install-Service.ps1`

- [x] **Scenario 5**: Configure Service Recovery Options
    - **Given** the service may crash or encounter errors
    - **When** the service is installed
    - **Then** recovery options are set to restart on failure (1st, 2nd, subsequent failures) using `sc.exe failure`
    - **Implementation**: Recovery configured to restart after 1 minute on all failures, reset after 24 hours

- [x] **Scenario 6**: Validate Configuration on Install
    - **Given** the service is being installed
    - **When** the installation script runs
    - **Then** the script validates that appsettings.json exists, RootPath is configured, and SQLite database path is valid before starting the service
    - **Implementation**: `Install-Service.ps1` includes `Validate-ConfigurationFile` function with `-ValidateOnly` option

- [x] **Scenario 7**: Provide Uninstall Script
    - **Given** the service needs to be removed
    - **When** I run the uninstall script
    - **Then** the service is stopped, deleted using `sc.exe delete`, and the installation folder is optionally removed
    - **Implementation**: `deployment/Uninstall-Service.ps1` with `-RemoveFiles` and `-RemoveData` options

- [x] **Scenario 8**: Log Installation and Service Events
    - **Given** the service is installed and started
    - **When** the service runs
    - **Then** service lifecycle events (start, stop, error) are logged to Windows Event Log for administrator visibility
    - **Implementation**: Windows Event Log configured in `Program.cs` (lines 65-71) with source "FolderToApiService"

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Publish command: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- Installation path: `C:\Program Files\<Company>\FolderToApi\FolderToApi.Service.exe`
- Service creation:
  ```
  sc.exe create "FolderToApiService" binpath= "C:\Program Files\<Company>\FolderToApi\FolderToApi.Service.exe" start=auto
  ```
- Recovery options:
  ```
  sc.exe failure "FolderToApiService" reset= 86400 actions= restart/60000/restart/60000/restart/60000
  ```
- Service account:
  - Option 1: Network Service (for testing)
  - Option 2: Custom service account (recommended for production)
  - Permissions: Read/Write to RootPath, SQLite DB, network access
- Installation script should:
  - Create installation directory
  - Copy executable and config files
  - Create service
  - Set permissions
  - Validate configuration
  - Start service
- Windows Event Log integration via `AddWindowsService()` in Program.cs
- Service name: configurable in appsettings.json or install script

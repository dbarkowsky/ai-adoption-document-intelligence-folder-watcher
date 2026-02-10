# US-001: Project Setup and .NET Worker Service Foundation

**As a** Developer,
**I want to** create a .NET Worker Service project with Windows Service hosting capabilities,
**So that** I have a solid foundation to build a long-running background service that runs as a Windows Service.

## Acceptance Criteria
- [ ] **Scenario 1**: Create Worker Service Project
    - **Given** I need to start the project
    - **When** I run `dotnet new worker --name FolderToApi.Service`
    - **Then** a new Worker Service project is created with Program.cs and Worker.cs

- [ ] **Scenario 2**: Add Windows Service Support
    - **Given** the project needs to run as a Windows Service
    - **When** I add the `Microsoft.Extensions.Hosting.WindowsServices` package and call `AddWindowsService()` in Program.cs
    - **Then** the application is configured to run as a Windows Service with proper Event Log integration

- [ ] **Scenario 3**: Configure Dependency Injection
    - **Given** the service needs multiple components
    - **When** I configure the Generic Host with DI container
    - **Then** all services can be registered and resolved via constructor injection

- [ ] **Scenario 4**: Verify Service Runs Locally
    - **Given** the basic project structure is complete
    - **When** I run the application locally with `dotnet run`
    - **Then** the hosted service starts, logs to console, and stops gracefully on Ctrl+C

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Target framework: .NET 8 or later on Windows Server
- Use Worker Service template which provides Generic Host + BackgroundService
- AddWindowsService() configures the host to work as a Windows Service and sets ServiceName
- This is the foundation for all subsequent user stories
- Package: `Microsoft.Extensions.Hosting.WindowsServices`

# US-020: Unit Testing Framework with Mocked Dependencies

**As a** Developer,
**I want to** create a comprehensive unit testing framework with mocked filesystem and HTTP dependencies,
**So that** all core components can be tested in isolation without external dependencies.

## Acceptance Criteria
- [ ] **Scenario 1**: Create Test Project Structure
    - **Given** the solution needs unit tests
    - **When** I create the test project
    - **Then** a new xUnit test project is created with references to the main service project and testing libraries (xUnit, Moq, FluentAssertions)

- [ ] **Scenario 2**: Mock Filesystem Operations
    - **Given** components need to interact with the filesystem
    - **When** tests are written
    - **Then** filesystem operations are abstracted behind IFileSystem interface and mocked using Moq or System.IO.Abstractions

- [ ] **Scenario 3**: Mock HTTP Client
    - **Given** the ApiClient needs to be tested without calling the real API
    - **When** HTTP tests are written
    - **Then** HttpClient is mocked using HttpMessageHandler or MockHttp library to simulate API responses

- [ ] **Scenario 4**: Mock Clock for Time-Based Logic
    - **Given** retry policies and backoff use time-based calculations
    - **When** time-sensitive tests are written
    - **Then** IClock abstraction is used and mocked to control time during tests

- [ ] **Scenario 5**: Test InboxEnumerator Filtering Logic
    - **Given** files need to be filtered by extension, size, and patterns
    - **When** unit tests run
    - **Then** the enumerator correctly filters files based on all configured rules (verified with multiple test cases)

- [ ] **Scenario 6**: Test CompletenessChecker Exclusive Open
    - **Given** files may be locked or incomplete
    - **When** unit tests simulate locked files
    - **Then** the checker correctly identifies Complete, Locked, TooNew, and Gone statuses

- [ ] **Scenario 7**: Test RetryPolicy Backoff Calculations
    - **Given** jobs fail and need retry scheduling
    - **When** unit tests provide various attempt counts
    - **Then** the retry policy calculates correct backoff delays, respects max attempts, and applies jitter

- [ ] **Scenario 8**: Test JobRepository CRUD Operations
    - **Given** job state needs to be persisted and queried
    - **When** unit tests use in-memory SQLite
    - **Then** all CRUD operations (create, update, query, mark sent/failed) work correctly with transactional semantics

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Test framework: xUnit (or NUnit/MSTest)
- Mocking library: Moq or NSubstitute
- Assertion library: FluentAssertions
- Filesystem abstraction: System.IO.Abstractions or custom IFileSystem interface
- HTTP mocking: MockHttp (RichardSzalay.MockHttp) or custom HttpMessageHandler
- In-memory SQLite for repository tests: `Data Source=:memory:`
- IClock abstraction for testable time:
  ```csharp
  public interface IClock { DateTime UtcNow { get; } }
  public class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
  public class MockClock : IClock { public DateTime UtcNow { get; set; } }
  ```
- All dependencies should be injected via constructor for testability
- Target: 80%+ code coverage for core business logic
- Test categories:
  - Unit tests: isolated component tests with mocks
  - Integration tests: database, filesystem, HTTP (separate project)

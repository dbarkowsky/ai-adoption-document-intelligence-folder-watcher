# US-011: API Client Implementation with Multipart Upload

**As a** Developer,
**I want to** implement an ApiClient that uploads files to the remote API with proper authentication and metadata,
**So that** files can be reliably delivered via HTTP POST with API key authentication.

## Acceptance Criteria
- [ ] **Scenario 1**: Upload File as Multipart Form Data
    - **Given** a file needs to be uploaded to the API
    - **When** the ApiClient sends the file
    - **Then** the request is sent as multipart/form-data with fields: `file` (binary), `filename` (string), and optional metadata fields (timestamp, source system)

- [ ] **Scenario 2**: Add API Key Header to Request
    - **Given** the API requires authentication
    - **When** the request is sent
    - **Then** the configured API key header (e.g., X-API-Key) is added to the request

- [ ] **Scenario 3**: Handle Successful Response (2xx)
    - **Given** the API responds with HTTP 200 or 201
    - **When** the response is received
    - **Then** the ApiClient returns a success result with HTTP status code and response time

- [ ] **Scenario 4**: Handle Retryable Errors (5xx, 408, 429)
    - **Given** the API responds with HTTP 500, 503, 408, or 429
    - **When** the response is received
    - **Then** the ApiClient returns a retryable error result with HTTP status code and error details

- [ ] **Scenario 5**: Handle Non-Retryable Errors (4xx)
    - **Given** the API responds with HTTP 400, 401, 403, 404, or 415
    - **When** the response is received
    - **Then** the ApiClient returns a permanent failure result indicating no retry should be attempted

- [ ] **Scenario 6**: Handle Network and Timeout Errors
    - **Given** a network failure or timeout occurs
    - **When** the request fails
    - **Then** the ApiClient catches the exception, logs it, and returns a retryable error result

- [ ] **Scenario 7**: Include Metadata in Request
    - **Given** additional metadata needs to be sent
    - **When** the multipart form is built
    - **Then** optional fields like `timestamp`, `source_system`, `job_id` are included in the form data

- [ ] **Scenario 8**: Log Request and Response Details
    - **Given** a request is sent
    - **When** the request completes
    - **Then** structured logs include: job_id, filename, HTTP method, URL, status code, response time, and any errors

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use IHttpClientFactory to get HttpClient instance
- Multipart form data (preferred):
  - Use MultipartFormDataContent
  - Add file as StreamContent
  - Add filename as StringContent
  - Add optional metadata fields
- Alternative: raw binary body with Content-Type header (Option B)
- Return ApiResult with:
  - Success: bool
  - IsRetryable: bool
  - HttpStatusCode: int?
  - ResponseTime: TimeSpan
  - ErrorMessage: string
- Retry conditions:
  - Network failures (HttpRequestException, TaskCanceledException)
  - HTTP 408, 429, 5xx
- Non-retryable: most 4xx (400, 401, 403, 404, 415)
- Log all requests and responses with structured logging
- Inject IHttpClientFactory and ILogger

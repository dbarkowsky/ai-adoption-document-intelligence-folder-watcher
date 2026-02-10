# US-010: HTTP Client Factory Setup with API Key Authentication

**As a** Developer,
**I want to** configure IHttpClientFactory with API key authentication and proper timeouts,
**So that** HTTP requests are centrally managed with correct authentication, timeouts, and handler lifetime management.

## Acceptance Criteria
- [ ] **Scenario 1**: Register Named HttpClient in DI
    - **Given** the service needs to call a remote API
    - **When** I configure services in Program.cs
    - **Then** AddHttpClient("RemoteApi", ...) is registered with base address, default headers, and timeouts configured

- [ ] **Scenario 2**: Configure Connection and Request Timeouts
    - **Given** the API may be slow or unresponsive
    - **When** the HttpClient is configured
    - **Then** the connection timeout is set to 10 seconds (default) and overall request timeout is set to 60 seconds (default), both configurable

- [ ] **Scenario 3**: Add API Key Header to All Requests
    - **Given** the API requires authentication via API key header
    - **When** a request is made
    - **Then** the X-API-Key header (or configured header name) is added to every request with the configured API key value

- [ ] **Scenario 4**: Use IHttpClientFactory to Create Clients
    - **Given** the ApiClient service needs an HttpClient
    - **When** IHttpClientFactory is injected
    - **Then** clients are created via CreateClient("RemoteApi") and are short-lived, while handlers are managed by the factory with proper lifetime (default 2 minutes)

- [ ] **Scenario 5**: Support HTTPS Endpoint URLs
    - **Given** the configuration specifies EndpointUrl
    - **When** the EndpointUrl starts with "https://"
    - **Then** the HttpClient uses HTTPS with proper TLS validation

- [ ] **Scenario 6**: Validate Endpoint URL on Startup
    - **Given** invalid or missing EndpointUrl can cause runtime failures
    - **When** the service starts
    - **Then** the EndpointUrl is validated (must be HTTPS, must be a valid URI) and the service fails fast if invalid

## Priority
- [x] High (Must Have)
- [ ] Medium (Should Have)
- [ ] Low (Nice to Have)

## Technical Notes / Assumptions
- Use IHttpClientFactory for centralized HttpClient management
- Register in Program.cs: `services.AddHttpClient("RemoteApi", client => { ... })`
- Configure:
  - BaseAddress from EndpointUrl
  - DefaultRequestHeaders: X-API-Key (configurable header name)
  - Timeout: configurable (default 60s)
- Handler lifetime is managed by IHttpClientFactory (default 2 minutes)
- Short-lived HttpClient instances from factory prevent DNS staleness and resource issues
- Future: prepare for DelegatingHandler for OAuth 2.0 token refresh and mTLS
- Configuration keys:
  - EndpointUrl (HTTPS required)
  - ApiKeyHeaderName (default "X-API-Key")
  - ApiKey (from secure storage)
  - HttpConnectTimeoutSeconds (default 10)
  - HttpRequestTimeoutSeconds (default 60)

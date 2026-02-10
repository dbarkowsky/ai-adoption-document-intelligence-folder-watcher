# US-028: Future Enhancement - OAuth 2.0 Client Credentials Flow

**As a** Developer,
**I want to** prepare the architecture for OAuth 2.0 client credentials authentication,
**So that** the service can be easily extended to support token-based authentication when required.

## Acceptance Criteria
- [ ] **Scenario 1**: Design DelegatingHandler for Token Management
    - **Given** OAuth 2.0 will be added in a future version
    - **When** the architecture is designed
    - **Then** a DelegatingHandler pattern is used for HTTP client middleware, making it easy to add token acquisition and refresh logic

- [ ] **Scenario 2**: Document OAuth 2.0 Requirements
    - **Given** OAuth 2.0 support is planned
    - **When** requirements are gathered
    - **Then** documentation specifies: token endpoint, client credentials (client ID/secret), scope, token caching strategy, and refresh logic

- [ ] **Scenario 3**: Add Configuration Placeholders
    - **Given** OAuth 2.0 configuration will be needed
    - **When** the configuration schema is defined
    - **Then** placeholders are added for: TokenEndpoint, ClientId, ClientSecret, Scope, TokenCacheExpirySeconds

- [ ] **Scenario 4**: Implement Token Cache Interface
    - **Given** tokens need to be cached and refreshed
    - **When** the token management is designed
    - **Then** an ITokenCache interface is defined with methods: GetToken(), RefreshToken(), and in-memory implementation is provided

- [ ] **Scenario 5**: Test OAuth Flow with Mock Token Server
    - **Given** OAuth support is implemented
    - **When** integration tests run
    - **Then** the service successfully acquires tokens, caches them, refreshes before expiry, and adds Authorization: Bearer header to requests

## Priority
- [ ] High (Must Have)
- [ ] Medium (Should Have)
- [x] Low (Nice to Have)

## Technical Notes / Assumptions
- OAuth 2.0 client credentials flow:
  1. Service sends: POST to TokenEndpoint with client_id, client_secret, grant_type=client_credentials, scope
  2. Token server responds: { "access_token": "...", "expires_in": 3600, "token_type": "Bearer" }
  3. Service caches token
  4. Service adds Authorization: Bearer <token> to API requests
  5. Service refreshes token before expiry
- Implementation approach:
  - Create OAuthTokenHandler : DelegatingHandler
  - Inject ITokenCache for token storage
  - Override SendAsync to add Authorization header
  - Implement token refresh logic with lock to prevent concurrent refreshes
- Configuration:
  - TokenEndpoint: URL to OAuth token endpoint
  - ClientId: OAuth client ID
  - ClientSecret: OAuth client secret (stored securely)
  - Scope: requested scope(s)
  - TokenCacheExpirySeconds: time to cache token before refresh (default 90% of expires_in)
- Security: ClientSecret must be stored securely (same as API key)
- This is explicitly deferred for v1 but architecture should support it

# US-029: Future Enhancement - mTLS Client Certificate Authentication

**As a** Developer,
**I want to** prepare the architecture for mTLS client certificate authentication,
**So that** the service can use client certificates from Windows certificate store when required.

## Acceptance Criteria
- [ ] **Scenario 1**: Design HttpClient Handler Configuration
    - **Given** mTLS will be added in a future version
    - **When** the architecture is designed
    - **Then** IHttpClientFactory is configured to support custom primary handler configuration via ConfigurePrimaryHttpMessageHandler

- [ ] **Scenario 2**: Document mTLS Requirements
    - **Given** mTLS support is planned
    - **When** requirements are gathered
    - **Then** documentation specifies: certificate store location (CurrentUser/LocalMachine), certificate thumbprint or subject name, and certificate validation behavior

- [ ] **Scenario 3**: Add Configuration Placeholders
    - **Given** mTLS configuration will be needed
    - **When** the configuration schema is defined
    - **Then** placeholders are added for: UseMtls (bool), CertificateThumbprint, CertificateStoreLocation, CertificateStoreName

- [ ] **Scenario 4**: Implement Certificate Loader
    - **Given** client certificates need to be loaded from Windows cert store
    - **When** mTLS is implemented
    - **Then** a CertificateLoader service finds and loads certificates by thumbprint or subject from the configured store

- [ ] **Scenario 5**: Configure SocketsHttpHandler with Client Certificate
    - **Given** client certificate is loaded
    - **When** HttpClient is created
    - **Then** the SocketsHttpHandler is configured with SslOptions.ClientCertificates containing the loaded certificate

- [ ] **Scenario 6**: Test mTLS with Mock Server
    - **Given** mTLS support is implemented
    - **When** integration tests run
    - **Then** the service successfully presents the client certificate during TLS handshake and the server validates it

## Priority
- [ ] High (Must Have)
- [ ] Medium (Should Have)
- [x] Low (Nice to Have)

## Technical Notes / Assumptions
- mTLS client certificate flow:
  1. Load certificate from Windows certificate store using thumbprint or subject
  2. Configure SocketsHttpHandler with client certificate
  3. TLS handshake includes client certificate
  4. Server validates client certificate
- Implementation approach:
  - Create CertificateLoader service
  - Load certificate: `X509Store.Open() → FindByThumbprint()`
  - Configure HttpClient:
    ```csharp
    services.AddHttpClient("RemoteApi")
        .ConfigurePrimaryHttpMessageHandler(() => {
            var handler = new SocketsHttpHandler();
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
            return handler;
        });
    ```
- Configuration:
  - UseMtls: bool (default false)
  - CertificateThumbprint: string (hex thumbprint)
  - CertificateStoreLocation: CurrentUser | LocalMachine
  - CertificateStoreName: My | Root | etc.
- Certificate permissions: service account must have read access to private key
- This is explicitly deferred for v1 but architecture should support it via IHttpClientFactory handler configuration

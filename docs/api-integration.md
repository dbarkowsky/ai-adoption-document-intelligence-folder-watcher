# API Integration Guide

Guide for API providers and integrators working with the Folder-to-API Delivery Service.

## Table of Contents

1. [Overview](#overview)
2. [HTTP Request Format](#http-request-format)
3. [Authentication](#authentication)
4. [Expected Response Codes](#expected-response-codes)
5. [Retry Behavior](#retry-behavior)
6. [Idempotency Expectations](#idempotency-expectations)
7. [Performance Characteristics](#performance-characteristics)
8. [Testing Your API](#testing-your-api)
9. [Example Implementations](#example-implementations)

---

## Overview

The Folder-to-API Delivery Service acts as an HTTP client that uploads files to your REST API endpoint. This document explains the service's behavior so you can implement compatible server-side logic.

### Client Behavior Summary

- **Method:** HTTP POST
- **Content-Type:** `multipart/form-data`
- **Authentication:** API key in custom header (default: `X-API-Key`)
- **Retry:** Automatic exponential backoff for transient failures
- **Timeout:** Configurable connect and request timeouts
- **User-Agent:** `FolderToApiService/1.0`

---

## HTTP Request Format

### Request Structure

```http
POST /upload HTTP/1.1
Host: api.example.com
X-API-Key: sk_live_abc123xyz789...
User-Agent: FolderToApiService/1.0
Content-Type: multipart/form-data; boundary=----WebKitFormBoundary...
Content-Length: 1234567

------WebKitFormBoundary...
Content-Disposition: form-data; name="file"; filename="document.pdf"
Content-Type: application/pdf

[Binary file content]
------WebKitFormBoundary...
Content-Disposition: form-data; name="filename"

document.pdf
------WebKitFormBoundary...--
```

### Multipart Form Fields

| Field Name | Type | Required | Description |
|------------|------|----------|-------------|
| `file` | Binary | Yes | The file contents |
| `filename` | String | Yes | Original filename |

### Headers

| Header | Value | Purpose |
|--------|-------|---------|
| `X-API-Key` | API key string | Authentication (header name configurable) |
| `User-Agent` | `FolderToApiService/1.0` | Identifies the client |
| `Content-Type` | `multipart/form-data` | Request body format |

---

## Authentication

### API Key Authentication (Current)

The service sends an API key in a custom HTTP header with each request.

**Default Header:** `X-API-Key`
**Configurable:** You can configure a different header name via `ApiClient.ApiKeyHeaderName` in appsettings.json

#### Server-Side Validation

```python
# Example: Python/Flask
@app.route('/upload', methods=['POST'])
def upload_file():
    api_key = request.headers.get('X-API-Key')

    if not api_key:
        return jsonify({'error': 'Missing API key'}), 401

    if not is_valid_api_key(api_key):
        return jsonify({'error': 'Invalid API key'}), 403

    # Process file upload...
    return jsonify({'status': 'success'}), 200
```

```csharp
// Example: ASP.NET Core
[HttpPost("upload")]
public IActionResult UploadFile()
{
    if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        return Unauthorized(new { error = "Missing API key" });

    if (!IsValidApiKey(apiKey))
        return StatusCode(403, new { error = "Invalid API key" });

    // Process file upload...
    return Ok(new { status = "success" });
}
```

### Future Authentication Methods

**OAuth 2.0 Client Credentials** (planned future enhancement):
- Service will request access tokens from your OAuth provider
- Tokens will be cached and refreshed automatically
- Requests will include `Authorization: Bearer <token>` header

**mTLS Client Certificates** (planned future enhancement):
- Service will present client certificate during TLS handshake
- Certificate loaded from Windows Certificate Store

---

## Expected Response Codes

### Success Responses

The service treats these as successful delivery:

| Status Code | Meaning | Service Action |
|-------------|---------|----------------|
| 200 OK | Success | Mark job as Sent, move file to sent/ |
| 201 Created | Resource created | Mark job as Sent, move file to sent/ |
| 202 Accepted | Accepted for processing | Mark job as Sent, move file to sent/ |
| 204 No Content | Success, no body | Mark job as Sent, move file to sent/ |

**Important:** Any 2xx response is treated as success.

### Retryable Errors

The service will retry these errors with exponential backoff:

| Status Code | Meaning | Service Action |
|-------------|---------|----------------|
| 408 Request Timeout | Server timeout | Retry with backoff |
| 429 Too Many Requests | Rate limit | Retry with backoff (honors Retry-After header if present) |
| 500 Internal Server Error | Server error | Retry with backoff |
| 502 Bad Gateway | Gateway error | Retry with backoff |
| 503 Service Unavailable | Temporarily unavailable | Retry with backoff |
| 504 Gateway Timeout | Gateway timeout | Retry with backoff |

**Network Errors (no response):**
- Connection timeout → Retry
- Connection refused → Retry
- DNS resolution failure → Retry
- TLS handshake failure → Retry

### Permanent Errors (No Retry)

The service will NOT retry these errors (immediate dead-letter):

| Status Code | Meaning | Service Action |
|-------------|---------|----------------|
| 400 Bad Request | Invalid request format | Move to failed/, no retry |
| 401 Unauthorized | Missing/invalid API key | Move to failed/, no retry |
| 403 Forbidden | Valid key, insufficient permission | Move to failed/, no retry |
| 404 Not Found | Endpoint not found | Move to failed/, no retry |
| 405 Method Not Allowed | POST not supported | Move to failed/, no retry |
| 413 Payload Too Large | File exceeds size limit | Move to failed/, no retry |
| 415 Unsupported Media Type | File type not accepted | Move to failed/, no retry |

**Best Practice:** Return specific 4xx codes for permanent errors to avoid unnecessary retries.

### Recommended Response Format

```json
{
  "status": "success",
  "message": "File uploaded successfully",
  "file_id": "abc123",
  "timestamp": "2026-02-09T10:30:00Z"
}
```

For errors:

```json
{
  "status": "error",
  "error": "InvalidFileType",
  "message": "Only PDF files are accepted",
  "timestamp": "2026-02-09T10:30:00Z"
}
```

---

## Retry Behavior

### Retry Schedule

**Default Configuration:**
- Max attempts: 10
- Max job age: 7 days
- Initial delay: 10 seconds
- Backoff multiplier: 2.0
- Max delay: 3600 seconds (1 hour)
- Jitter: ±10%

**Example Retry Timeline:**

| Attempt | Delay | Cumulative Time |
|---------|-------|-----------------|
| 1 (initial) | 0s | 0s |
| 2 | ~10s | ~10s |
| 3 | ~20s | ~30s |
| 4 | ~40s | ~1m 10s |
| 5 | ~80s | ~2m 30s |
| 6 | ~160s | ~5m |
| 7 | ~320s | ~10m |
| 8 | ~640s | ~21m |
| 9 | ~1280s | ~42m |
| 10 | ~2560s | ~1h 24m |

### Dead-Letter Conditions

Jobs are moved to dead-letter (failed/) when ANY of these conditions are met:

1. **Max attempts exceeded** (default: 10)
2. **Max job age exceeded** (default: 7 days)
3. **Permanent HTTP error** (4xx, see above)

### Retry-After Header Support

If your API returns `429 Too Many Requests` with a `Retry-After` header, the service will honor it:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 120
```

The service will wait at least 120 seconds before retrying (or longer if exponential backoff is longer).

---

## Idempotency Expectations

### Current Behavior (v1)

The service does **NOT** currently send idempotency keys. Your API **MUST** handle duplicate requests gracefully.

**Why Duplicates Can Occur:**
- Service restart during upload
- Network interruption after server processes but before client receives response
- Retry after ambiguous error (connection reset)

### Recommended Server-Side Handling

#### Option 1: Natural Idempotency

If your API naturally handles duplicates (e.g., content-based deduplication), no special handling needed.

```python
# Example: Store files by hash
file_hash = hashlib.sha256(file_content).hexdigest()
if file_exists(file_hash):
    return jsonify({'status': 'success', 'duplicate': True}), 200
else:
    store_file(file_hash, file_content)
    return jsonify({'status': 'success', 'duplicate': False}), 201
```

#### Option 2: Request ID Tracking

Track recent successful uploads to detect duplicates:

```python
# Example: Cache recent uploads
@app.route('/upload', methods=['POST'])
def upload_file():
    file_name = request.form['filename']
    file_size = len(request.files['file'].read())
    request_signature = f"{file_name}:{file_size}"

    # Check if recently processed (e.g., last 1 hour)
    if redis_client.get(f"upload:{request_signature}"):
        return jsonify({'status': 'success', 'note': 'Already processed'}), 200

    # Process upload...
    redis_client.setex(f"upload:{request_signature}", 3600, "1")
    return jsonify({'status': 'success'}), 201
```

### Future Enhancement

**Client-Side Idempotency Keys** (planned):
- Service will generate unique idempotency key per job
- Key will be sent in header: `Idempotency-Key: <uuid>`
- Server can use this key to identify duplicate requests

---

## Performance Characteristics

### Request Patterns

**Throughput:** Up to 500 files/hour (configurable via scan interval)

**Concurrency:** Up to 5 concurrent uploads (default, configurable)

**File Sizes:** Typically 1-50 MB (configurable maximum)

**Request Timeouts:**
- Connect timeout: 10 seconds (default)
- Request timeout: 60 seconds (default)
- Configurable up to 10 minutes

### Server Requirements

Your API should be able to handle:

- **Sustained throughput:** At least 5 requests/minute (500 files/hour = ~8/minute)
- **Burst capacity:** Up to 5 simultaneous uploads
- **Large payloads:** Up to 50 MB per request (default, may be configured higher)
- **Long-running requests:** Uploads may take 30-60 seconds for large files over slow connections

### Recommended API Limits

```
Rate Limit: 100 requests/minute per API key
Payload Size: 100 MB maximum
Request Timeout: 120 seconds server-side
Connection Pool: 10-20 concurrent connections per client
```

---

## Testing Your API

### Manual Testing with curl

```bash
# Test basic upload
curl -X POST https://api.example.com/upload \
  -H "X-API-Key: your-api-key" \
  -F "file=@test-document.pdf" \
  -F "filename=test-document.pdf"

# Test with large file
curl -X POST https://api.example.com/upload \
  -H "X-API-Key: your-api-key" \
  -F "file=@large-file.pdf" \
  -F "filename=large-file.pdf" \
  --max-time 120

# Test authentication failure
curl -X POST https://api.example.com/upload \
  -H "X-API-Key: invalid-key" \
  -F "file=@test.pdf" \
  -F "filename=test.pdf"
```

### Testing with PowerShell

```powershell
# Test basic upload
$apiKey = "your-api-key"
$endpoint = "https://api.example.com/upload"
$filePath = "C:\Test\document.pdf"

$boundary = [System.Guid]::NewGuid().ToString()
$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"$(Split-Path $filePath -Leaf)`"",
    "Content-Type: application/pdf",
    "",
    [System.IO.File]::ReadAllText($filePath),
    "--$boundary",
    "Content-Disposition: form-data; name=`"filename`"",
    "",
    "$(Split-Path $filePath -Leaf)",
    "--$boundary--"
)

$body = $bodyLines -join "`r`n"

try {
    $response = Invoke-WebRequest -Uri $endpoint `
        -Method POST `
        -Headers @{"X-API-Key" = $apiKey} `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $body

    Write-Host "Success: HTTP $($response.StatusCode)" -ForegroundColor Green
    Write-Host $response.Content
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
```

### Load Testing

```bash
# Install Apache Bench (ab)
# Test 100 requests with 5 concurrent connections
ab -n 100 -c 5 -T 'multipart/form-data' -p test-file.pdf -H "X-API-Key: your-key" https://api.example.com/upload
```

### Integration Testing Checklist

- [ ] Upload small file (< 1 MB)
- [ ] Upload large file (near maximum size limit)
- [ ] Upload with valid API key → expect 200/201
- [ ] Upload with invalid API key → expect 401/403
- [ ] Upload with missing API key → expect 401
- [ ] Upload unsupported file type → expect 415 (if applicable)
- [ ] Upload when rate limited → expect 429 with Retry-After
- [ ] Upload when server down → expect 500/503
- [ ] Verify duplicate upload handling (same file twice)
- [ ] Measure response times under load

---

## Example Implementations

### Python/Flask

```python
from flask import Flask, request, jsonify
import os
import hashlib

app = Flask(__name__)

VALID_API_KEYS = {'sk_live_abc123': 'client1'}
UPLOAD_FOLDER = '/var/uploads'

@app.route('/upload', methods=['POST'])
def upload_file():
    # 1. Authentication
    api_key = request.headers.get('X-API-Key')
    if not api_key or api_key not in VALID_API_KEYS:
        return jsonify({'error': 'Invalid API key'}), 403

    # 2. Validate request
    if 'file' not in request.files:
        return jsonify({'error': 'No file provided'}), 400

    file = request.files['file']
    filename = request.form.get('filename', file.filename)

    if not filename:
        return jsonify({'error': 'Filename required'}), 400

    # 3. Validate file type (example)
    allowed_extensions = {'.pdf', '.docx', '.jpg'}
    ext = os.path.splitext(filename)[1].lower()
    if ext not in allowed_extensions:
        return jsonify({'error': f'Unsupported file type: {ext}'}), 415

    # 4. Save file
    file_content = file.read()
    file_hash = hashlib.sha256(file_content).hexdigest()
    save_path = os.path.join(UPLOAD_FOLDER, f"{file_hash}_{filename}")

    with open(save_path, 'wb') as f:
        f.write(file_content)

    # 5. Success response
    return jsonify({
        'status': 'success',
        'file_id': file_hash,
        'filename': filename,
        'size': len(file_content)
    }), 201

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)
```

### Node.js/Express

```javascript
const express = require('express');
const multer = require('multer');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const app = express();
const upload = multer({ dest: '/tmp/uploads' });

const VALID_API_KEYS = new Set(['sk_live_abc123']);
const UPLOAD_FOLDER = '/var/uploads';

app.post('/upload', upload.single('file'), (req, res) => {
    // 1. Authentication
    const apiKey = req.headers['x-api-key'];
    if (!apiKey || !VALID_API_KEYS.has(apiKey)) {
        return res.status(403).json({ error: 'Invalid API key' });
    }

    // 2. Validate request
    if (!req.file) {
        return res.status(400).json({ error: 'No file provided' });
    }

    const filename = req.body.filename || req.file.originalname;

    // 3. Validate file type
    const allowedExt = ['.pdf', '.docx', '.jpg'];
    const ext = path.extname(filename).toLowerCase();
    if (!allowedExt.includes(ext)) {
        return res.status(415).json({ error: `Unsupported file type: ${ext}` });
    }

    // 4. Save file
    const fileContent = fs.readFileSync(req.file.path);
    const fileHash = crypto.createHash('sha256').update(fileContent).digest('hex');
    const savePath = path.join(UPLOAD_FOLDER, `${fileHash}_${filename}`);

    fs.renameSync(req.file.path, savePath);

    // 5. Success response
    res.status(201).json({
        status: 'success',
        file_id: fileHash,
        filename: filename,
        size: fileContent.length
    });
});

app.listen(5000, () => {
    console.log('Server running on port 5000');
});
```

### ASP.NET Core

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

[ApiController]
[Route("api")]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> _logger;
    private readonly string _uploadFolder = "/var/uploads";
    private readonly HashSet<string> _validApiKeys = new() { "sk_live_abc123" };
    private readonly HashSet<string> _allowedExtensions = new() { ".pdf", ".docx", ".jpg" };

    public UploadController(ILogger<UploadController> logger)
    {
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string filename)
    {
        // 1. Authentication
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey) ||
            !_validApiKeys.Contains(apiKey))
        {
            return StatusCode(403, new { error = "Invalid API key" });
        }

        // 2. Validate request
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        filename ??= file.FileName;

        // 3. Validate file type
        var ext = Path.GetExtension(filename).ToLower();
        if (!_allowedExtensions.Contains(ext))
        {
            return StatusCode(415, new { error = $"Unsupported file type: {ext}" });
        }

        // 4. Save file
        using var stream = file.OpenReadStream();
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        var fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        var savePath = Path.Combine(_uploadFolder, $"{fileHash}_{filename}");
        stream.Position = 0;

        using var fileStream = new FileStream(savePath, FileMode.Create);
        await stream.CopyToAsync(fileStream);

        // 5. Success response
        return StatusCode(201, new
        {
            status = "success",
            file_id = fileHash,
            filename = filename,
            size = file.Length
        });
    }
}
```

---

## Next Steps

- **[Architecture Documentation](architecture.md)** - Understand the client's internal architecture
- **[Security Guide](security.md)** - Security best practices for API authentication
- **[Configuration Reference](configuration.md)** - Configure client behavior
- **[Troubleshooting Guide](troubleshooting.md)** - Resolve integration issues

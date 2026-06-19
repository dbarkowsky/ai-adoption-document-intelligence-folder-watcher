using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FolderToApi.Service.Services;

/// <summary>
/// DelegatingHandler that logs outgoing HTTP requests and their responses at Debug level.
/// Enable by setting the minimum log level to Debug, e.g. in appsettings.Development.json:
///   "Logging": { "MinimumLevel": "Debug" }
/// The API key value is partially masked (first 4 / last 4 chars) to help confirm
/// the correct key is being sent without exposing the full secret in logs.
/// </summary>
public class RequestLoggingHandler : DelegatingHandler
{
    private readonly ILogger<RequestLoggingHandler> _logger;

    public RequestLoggingHandler(ILogger<RequestLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var headers = BuildHeaderSummary(request);
            string? body = null;

            if (request.Content is not null)
            {
                // Read and buffer so the actual send can still use the content
                var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);

                // Capture original content headers BEFORE replacing the content object
                var originalContentHeaders = request.Content.Headers
                    .Select(h => (h.Key, Values: h.Value.ToList()))
                    .ToList();

                request.Content = new ByteArrayContent(bytes);

                // Restore original content headers (Content-Type etc.) to the new content
                foreach (var (key, values) in originalContentHeaders)
                {
                    request.Content.Headers.TryAddWithoutValidation(key, values);
                }

                // Log full JSON body with base64 file value redacted
                body = RedactFileField(Encoding.UTF8.GetString(bytes));
            }

            _logger.LogDebug(
                "Outgoing HTTP {Method} {Url}\nHeaders:\n{Headers}{BodySection}",
                request.Method,
                request.RequestUri,
                headers,
                body is null ? string.Empty : $"\nBody:\n{body}");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var responseHeaders = BuildResponseHeaderSummary(response);

            // Always log response body on errors so we can see the full backend message
            string responseBodySection = string.Empty;
            if (!response.IsSuccessStatusCode && response.Content is not null)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                // Re-buffer so callers can still read it
                response.Content = new StringContent(responseBody,
                    Encoding.UTF8,
                    response.Content.Headers.ContentType?.MediaType ?? "application/json");
                responseBodySection = $"\nResponse Body:\n{responseBody}";
            }

            _logger.LogDebug(
                "Incoming HTTP {StatusCode} {ReasonPhrase}\nResponse headers:\n{Headers}{ResponseBody}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                responseHeaders,
                responseBodySection);
        }

        return response;
    }

    private static string BuildHeaderSummary(HttpRequestMessage request)
    {
        var sb = new StringBuilder();

        foreach (var header in request.Headers)
        {
            var values = header.Value.ToList();
            var display = string.Join(", ", values.Select(v => MaskIfSensitive(header.Key, v)));
            sb.AppendLine($"  {header.Key}: {display}");
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        return sb.ToString();
    }

    private static string BuildResponseHeaderSummary(HttpResponseMessage response)
    {
        var sb = new StringBuilder();

        foreach (var header in response.Headers)
        {
            sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
        }

        // WWW-Authenticate is especially useful for diagnosing 401s
        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                sb.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses the JSON body and replaces the "file" field value with a placeholder
    /// so the full payload is visible in logs without flooding them with base64 data.
    /// Falls back to raw truncation if the body is not valid JSON.
    /// </summary>
    private static string RedactFileField(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            if (node is JsonObject obj && obj["file"] is JsonValue fileVal)
            {
                var raw = fileVal.GetValue<string>();
                obj["file"] = $"[base64 data, {raw.Length} chars]";
            }
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? body;
        }
        catch
        {
            // Not JSON or parse failed — fall back to truncation
            return body.Length > 500 ? body[..500] + " …[truncated]" : body;
        }
    }

    /// <summary>
    /// Masks header values that look like API keys / tokens so the full secret
    /// is never written to log files. Shows first 4 and last 4 characters.
    /// </summary>
    private static string MaskIfSensitive(string headerName, string value)
    {
        var lower = headerName.ToLowerInvariant();
        bool isSensitive = lower.Contains("key") ||
                           lower.Contains("token") ||
                           lower.Contains("secret") ||
                           lower.Contains("authorization");

        if (!isSensitive || value.Length <= 8)
            return value;

        return $"{value[..4]}{"*".PadRight(value.Length - 8, '*')}{value[^4..]} ({value.Length} chars)";
    }
}

using System.Text;

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
                request.Content = new ByteArrayContent(bytes);

                // Restore original headers (Content-Type etc.)
                foreach (var h in request.Content.Headers)
                {
                    request.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }

                // Only include the first 500 chars of the body to avoid flooding the log
                body = Encoding.UTF8.GetString(bytes);
                if (body.Length > 500)
                    body = body[..500] + " …[truncated]";
            }

            _logger.LogDebug(
                "Outgoing HTTP {Method} {Url}\nHeaders:\n{Headers}{BodySection}",
                request.Method,
                request.RequestUri,
                headers,
                body is null ? string.Empty : $"\nBody (first 500 chars):\n{body}");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var responseHeaders = BuildResponseHeaderSummary(response);
            _logger.LogDebug(
                "Incoming HTTP {StatusCode} {ReasonPhrase}\nResponse headers:\n{Headers}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                responseHeaders);
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

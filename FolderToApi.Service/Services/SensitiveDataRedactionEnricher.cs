using Serilog.Core;
using Serilog.Events;

namespace FolderToApi.Service.Services;

/// <summary>
/// Serilog enricher that redacts sensitive data from log events
/// </summary>
public class SensitiveDataRedactionEnricher : ILogEventEnricher
{
    private static readonly string[] SensitivePropertyNames =
    {
        "ApiKey",
        "api_key",
        "X-API-Key",
        "Authorization",
        "authorization",
        "Bearer",
        "Password",
        "password",
        "Secret",
        "secret",
        "Token",
        "token",
        "Credential",
        "credential"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Check all properties for sensitive data
        var propertiesToRedact = new List<string>();

        foreach (var property in logEvent.Properties)
        {
            if (IsSensitiveProperty(property.Key))
            {
                propertiesToRedact.Add(property.Key);
            }
            else if (property.Value is ScalarValue scalarValue &&
                     scalarValue.Value is string stringValue)
            {
                // Check if the value looks like an API key or token (heuristic)
                if (LooksLikeSensitiveValue(stringValue))
                {
                    propertiesToRedact.Add(property.Key);
                }
            }
        }

        // Redact sensitive properties
        foreach (var propertyName in propertiesToRedact)
        {
            logEvent.RemovePropertyIfPresent(propertyName);
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty(propertyName, "[REDACTED]"));
        }
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        return SensitivePropertyNames.Any(sensitive =>
            propertyName.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeSensitiveValue(string value)
    {
        // Very long alphanumeric strings might be API keys or tokens
        if (value.Length > 32 && IsAlphanumericOrBase64(value))
        {
            return true;
        }

        // Bearer tokens
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsAlphanumericOrBase64(string value)
    {
        return value.All(c => char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=');
    }
}

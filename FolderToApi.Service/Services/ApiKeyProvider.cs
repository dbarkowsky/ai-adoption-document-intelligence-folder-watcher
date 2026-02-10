using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FolderToApi.Service.Services;

/// <summary>
/// Provides API keys from various secure sources
/// </summary>
public class ApiKeyProvider : IApiKeyProvider
{
    private readonly ApiClientOptions _options;
    private readonly ILogger<ApiKeyProvider> _logger;

    public ApiKeyProvider(IOptions<ApiClientOptions> options, ILogger<ApiKeyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string GetApiKey()
    {
        return _options.ApiKeySource.ToLowerInvariant() switch
        {
            "configuration" => GetFromConfiguration(),
            "credentialmanager" => GetFromCredentialManager(),
            "dpapi" => GetFromDpapi(),
            _ => throw new InvalidOperationException($"Unknown ApiKeySource: {_options.ApiKeySource}")
        };
    }

    private string GetFromConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("ApiKey is not configured in appsettings");
        }

        return _options.ApiKey;
    }

    private string GetFromCredentialManager()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only supported on Windows");
        }

        if (string.IsNullOrWhiteSpace(_options.CredentialName))
        {
            throw new InvalidOperationException("CredentialName must be specified when using CredentialManager");
        }

        // TODO: Implement Windows Credential Manager integration using P/Invoke
        // For now, this is a placeholder that will be implemented when running on Windows
        _logger.LogWarning("Windows Credential Manager integration not yet implemented. Using configuration fallback.");
        return GetFromConfiguration();
    }

    private string GetFromDpapi()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("DPAPI is only supported on Windows");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("ApiKey (encrypted) is not configured for DPAPI");
        }

#if WINDOWS
        // Assuming the ApiKey in config is a Base64-encoded DPAPI-protected value
        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(_options.ApiKey);
            byte[] decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt API key using DPAPI");
            throw new InvalidOperationException("Failed to decrypt API key using DPAPI", ex);
        }
#else
        // DPAPI not available on this platform, fall back to configuration
        _logger.LogWarning("DPAPI requested but not available on this platform. Using configuration fallback.");
        return GetFromConfiguration();
#endif
    }
}

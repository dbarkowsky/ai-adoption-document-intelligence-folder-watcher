namespace FolderToApi.Service.Services;

/// <summary>
/// Provides secure access to API keys from various sources
/// </summary>
public interface IApiKeyProvider
{
    /// <summary>
    /// Retrieves the API key from the configured source
    /// </summary>
    /// <returns>The API key value</returns>
    string GetApiKey();
}

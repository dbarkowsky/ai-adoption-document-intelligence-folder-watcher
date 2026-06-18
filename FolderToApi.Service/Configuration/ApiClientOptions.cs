using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class ApiClientOptions
{
    public const string SectionName = "ApiClient";

    [Required(ErrorMessage = "EndpointUrl is required")]
    [Url(ErrorMessage = "EndpointUrl must be a valid URL")]
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// Source for API key: "Configuration", "CredentialManager", or "DPAPI"
    /// </summary>
    public string ApiKeySource { get; set; } = "Configuration";

    /// <summary>
    /// API key value (when ApiKeySource is "Configuration")
    /// For production, use CredentialManager or DPAPI
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the credential in Windows Credential Manager (when ApiKeySource is "CredentialManager")
    /// </summary>
    public string? CredentialName { get; set; }

    /// <summary>
    /// Header name for the API key (default: X-API-Key)
    /// </summary>
    [Required]
    public string ApiKeyHeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Model identifier sent in upload payload as model_id.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow config identifier sent in upload payload as workflow_config_id.
    /// </summary>
    public string WorkflowConfigId { get; set; } = string.Empty;

    [Range(1, 300, ErrorMessage = "ConnectTimeoutSeconds must be between 1 and 300")]
    public int ConnectTimeoutSeconds { get; set; } = 10;

    [Range(1, 600, ErrorMessage = "RequestTimeoutSeconds must be between 1 and 600")]
    public int RequestTimeoutSeconds { get; set; } = 60;
}

namespace FolderToApi.Service.Services;

/// <summary>
/// Coordinates startup readiness between hosted services.
/// </summary>
public interface IStartupInitializationState
{
    Task<bool> WaitForInitializationAsync(CancellationToken cancellationToken);
    void MarkInitialized();
    void MarkFailed();
}

namespace FolderToApi.Service.Services;

/// <summary>
/// Shared startup readiness state for hosted services.
/// </summary>
public class StartupInitializationState : IStartupInitializationState
{
    private readonly TaskCompletionSource<bool> _initializationTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> WaitForInitializationAsync(CancellationToken cancellationToken)
    {
        return _initializationTcs.Task.WaitAsync(cancellationToken);
    }

    public void MarkInitialized()
    {
        _initializationTcs.TrySetResult(true);
    }

    public void MarkFailed()
    {
        _initializationTcs.TrySetResult(false);
    }
}

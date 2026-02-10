namespace FolderToApi.Service.Services;

/// <summary>
/// Production implementation of IClock that returns real system time
/// </summary>
public class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}

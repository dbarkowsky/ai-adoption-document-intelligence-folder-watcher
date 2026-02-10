namespace FolderToApi.Service.Services;

/// <summary>
/// Abstraction for current time, enabling testable time-dependent logic
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current UTC time
    /// </summary>
    DateTime UtcNow { get; }
}

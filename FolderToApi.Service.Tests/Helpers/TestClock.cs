namespace FolderToApi.Service.Tests.Helpers;

/// <summary>
/// Test implementation of IClock with controllable time for deterministic testing
/// </summary>
public class TestClock : IClock
{
    private DateTime _currentTime;

    public TestClock(DateTime? startTime = null)
    {
        _currentTime = startTime ?? DateTime.UtcNow;
    }

    public DateTime UtcNow => _currentTime;

    /// <summary>
    /// Advance the clock by a specific duration
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }

    /// <summary>
    /// Set the clock to a specific time
    /// </summary>
    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }
}

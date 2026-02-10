using FolderToApi.Service.Tests.Helpers;

namespace FolderToApi.Service.Tests.Services;

public class CompletenessCheckerTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public void CheckFile_ReturnsComplete_WhenFileIsReady()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddFile("/inbox/test.pdf", new MockFileData("content"));

        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 5
        });

        var clock = new TestClock(DateTime.UtcNow);
        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, mockFileSystem, mockLogger.Object);

        // Simulate file created 10 seconds ago (older than stable age)
        var fileInfo = mockFileSystem.FileInfo.New("/inbox/test.pdf");
        mockFileSystem.File.SetLastWriteTime("/inbox/test.pdf", clock.UtcNow.AddSeconds(-10));

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.Status.Should().Be(FileCompletenessStatus.Complete);
    }

    [Fact]
    public void CheckFile_ReturnsTooNew_WhenWithinStableAgeWindow()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddFile("/inbox/test.pdf", new MockFileData("content"));

        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 10
        });

        var clock = new TestClock(DateTime.UtcNow);
        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, mockFileSystem, mockLogger.Object);

        // Simulate file created 3 seconds ago (within stable age window)
        var fileInfo = mockFileSystem.FileInfo.New("/inbox/test.pdf");
        mockFileSystem.File.SetLastWriteTime("/inbox/test.pdf", clock.UtcNow.AddSeconds(-3));

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.Status.Should().Be(FileCompletenessStatus.TooNew);
    }

    [Fact]
    public void CheckFile_ReturnsGone_WhenFileDoesNotExist()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        // Don't add the file

        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 5
        });

        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, mockFileSystem, mockLogger.Object);

        var fileInfo = mockFileSystem.FileInfo.New("/inbox/missing.pdf");

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.Status.Should().Be(FileCompletenessStatus.Gone);
    }

    [Fact]
    public void CheckFile_ReturnsLocked_WhenFileIsLocked()
    {
        // Arrange - Use real temp file for lock testing since MockFileSystem can't simulate locks
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-locked-{Guid.NewGuid()}.pdf");
        _tempFiles.Add(tempFile);
        File.WriteAllText(tempFile, "test content");
        File.SetLastWriteTime(tempFile, DateTime.UtcNow.AddSeconds(-10)); // Old enough

        var realFileSystem = new FileSystem();
        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 5
        });

        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, realFileSystem, mockLogger.Object);

        var fileInfo = realFileSystem.FileInfo.New(tempFile);

        // Lock the file
        using var lockingStream = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.Status.Should().Be(FileCompletenessStatus.Locked);
    }

    [Fact]
    public void CheckFile_SetsCheckedAtUtc()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddFile("/inbox/test.pdf", new MockFileData("content"));

        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 5
        });

        var clock = new TestClock(DateTime.UtcNow);
        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, mockFileSystem, mockLogger.Object);

        var fileInfo = mockFileSystem.FileInfo.New("/inbox/test.pdf");
        mockFileSystem.File.SetLastWriteTime("/inbox/test.pdf", clock.UtcNow.AddSeconds(-10));

        var beforeCheck = DateTime.UtcNow;

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.CheckedAtUtc.Should().BeCloseTo(beforeCheck, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CheckFile_ReturnsComplete_WhenFileCanBeOpenedExclusively()
    {
        // Arrange - Use real temp file to test actual file locking
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-unlocked-{Guid.NewGuid()}.pdf");
        _tempFiles.Add(tempFile);
        File.WriteAllText(tempFile, "test content");
        File.SetLastWriteTime(tempFile, DateTime.UtcNow.AddSeconds(-10)); // Old enough

        var realFileSystem = new FileSystem();
        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 5
        });

        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, realFileSystem, mockLogger.Object);

        var fileInfo = realFileSystem.FileInfo.New(tempFile);

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert
        result.Status.Should().Be(FileCompletenessStatus.Complete);
    }

    [Fact]
    public void CheckFile_StableAgeCalculation_UsesLastWriteTime()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddFile("/inbox/test.pdf", new MockFileData("content"));

        var options = Options.Create(new FolderWatcherOptions
        {
            StableAgeSeconds = 60
        });

        var mockLogger = new Mock<ILogger<CompletenessChecker>>();
        var checker = new CompletenessChecker(options, mockFileSystem, mockLogger.Object);

        var fileInfo = mockFileSystem.FileInfo.New("/inbox/test.pdf");

        // Test with file exactly at boundary (60 seconds old)
        mockFileSystem.File.SetLastWriteTime("/inbox/test.pdf", DateTime.UtcNow.AddSeconds(-60));

        // Act
        var result = checker.CheckFile(fileInfo);

        // Assert - Should be Complete since it's >= StableAgeSeconds
        result.Status.Should().Be(FileCompletenessStatus.Complete);
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

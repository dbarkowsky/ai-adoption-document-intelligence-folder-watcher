namespace FolderToApi.Service.Tests.Services;

public class InboxEnumeratorTests
{
    [Fact]
    public void EnumerateEligibleFiles_FiltersHiddenFiles()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");
        mockFileSystem.AddFile("/inbox/visible.pdf", new MockFileData("content"));
        mockFileSystem.AddFile("/inbox/hidden.pdf", new MockFileData("content")
        {
            Attributes = FileAttributes.Hidden
        });

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" }
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().HaveCount(1);
        files.Should().ContainSingle(f => f.Name == "visible.pdf");
    }

    [Fact]
    public void EnumerateEligibleFiles_FiltersSystemFiles()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");
        mockFileSystem.AddFile("/inbox/normal.pdf", new MockFileData("content"));
        mockFileSystem.AddFile("/inbox/system.pdf", new MockFileData("content")
        {
            Attributes = FileAttributes.System
        });

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" }
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().HaveCount(1);
        files.Should().ContainSingle(f => f.Name == "normal.pdf");
    }

    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".PDF", true)]  // Case insensitive
    [InlineData(".txt", false)]
    [InlineData(".doc", false)]
    public void EnumerateEligibleFiles_FiltersByExtension(string extension, bool shouldInclude)
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");
        mockFileSystem.AddFile($"/inbox/file{extension}", new MockFileData("content"));

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" }
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        if (shouldInclude)
        {
            files.Should().HaveCount(1);
        }
        else
        {
            files.Should().BeEmpty();
        }
    }

    [Fact]
    public void EnumerateEligibleFiles_FiltersOversizedFiles()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");

        // Small file (1 KB)
        mockFileSystem.AddFile("/inbox/small.pdf", new MockFileData(new byte[1024]));

        // Large file (100 MB - exceeds 50 MB limit)
        mockFileSystem.AddFile("/inbox/large.pdf", new MockFileData(new byte[100 * 1024 * 1024]));

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" },
            MaxFileSizeMB = 50
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().HaveCount(1);
        files.Should().ContainSingle(f => f.Name == "small.pdf");
    }

    [Fact]
    public void EnumerateEligibleFiles_FiltersZeroByteFiles_WhenConfigured()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");
        mockFileSystem.AddFile("/inbox/normal.pdf", new MockFileData("content"));
        mockFileSystem.AddFile("/inbox/empty.pdf", new MockFileData(Array.Empty<byte>()));

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" },
            SkipZeroByteFiles = true
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().HaveCount(1);
        files.Should().ContainSingle(f => f.Name == "normal.pdf");
    }

    [Fact]
    public void EnumerateEligibleFiles_HandlesDirectoryNotFound()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        // Don't create the inbox directory

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/nonexistent");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" }
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEligibleFiles_ReturnsMultipleEligibleFiles()
    {
        // Arrange
        var mockFileSystem = new MockFileSystem();
        mockFileSystem.AddDirectory("/inbox");
        mockFileSystem.AddFile("/inbox/file1.pdf", new MockFileData("content1"));
        mockFileSystem.AddFile("/inbox/file2.pdf", new MockFileData("content2"));
        mockFileSystem.AddFile("/inbox/file3.pdf", new MockFileData("content3"));

        var mockFolderService = new Mock<IFolderStructureService>();
        mockFolderService.Setup(x => x.InboxPath).Returns("/inbox");

        var options = Options.Create(new FolderWatcherOptions
        {
            AllowedExtensions = new[] { ".pdf" }
        });

        var mockLogger = new Mock<ILogger<InboxEnumerator>>();
        var enumerator = new InboxEnumerator(options, mockFolderService.Object, mockFileSystem, mockLogger.Object);

        // Act
        var files = enumerator.EnumerateEligibleFiles().ToList();

        // Assert
        files.Should().HaveCount(3);
    }
}

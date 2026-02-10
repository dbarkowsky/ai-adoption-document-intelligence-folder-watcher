using System.Net;
using System.Net.Http;
using Moq.Protected;

namespace FolderToApi.Service.Tests.Services;

public class ApiClientTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public async Task UploadFileAsync_ReturnsSuccessful_On2xxStatus()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "{\"message\": \"success\"}");
        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeTrue();
        result.HttpStatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(429)]
    [InlineData(408)]
    public async Task UploadFileAsync_ReturnsRetryable_OnRetryableStatusCodes(int statusCode)
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler((HttpStatusCode)statusCode, "error");
        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRetryable.Should().BeTrue();
        result.HttpStatusCode.Should().Be(statusCode);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(415)]
    public async Task UploadFileAsync_ReturnsPermanentError_On4xxStatusCodes(int statusCode)
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler((HttpStatusCode)statusCode, "error");
        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRetryable.Should().BeFalse();
        result.HttpStatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsRetryable_OnHttpRequestException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRetryable.Should().BeTrue();
        result.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsRetryable_OnTaskCanceledException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRetryable.Should().BeTrue();
        result.ErrorMessage.Should().ContainAny("timeout", "Timeout");
    }

    [Fact]
    public async Task UploadFileAsync_MeasuresResponseTime()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate 100ms delay
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                };
            });

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.ResponseTime.TotalMilliseconds.Should().BeGreaterOrEqualTo(50); // At least 50ms due to delay
    }

    [Fact]
    public async Task UploadFileAsync_SendsMultipartFormData()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        await client.UploadFileAsync(job, tempFile);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().BeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task UploadFileAsync_IncludesJobMetadata()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedContent = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequest = req;
                // Capture content before it gets disposed
                if (req.Content != null)
                {
                    capturedContent = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        await client.UploadFileAsync(job, tempFile);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Content.Should().BeOfType<MultipartFormDataContent>();

        // Verify multipart form contains the job_id
        capturedContent.Should().NotBeNull();
        capturedContent.Should().Contain(job.JobId);
    }

    [Fact]
    public async Task UploadFileAsync_ReturnsPermanentError_OnUnexpectedException()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var mockFactory = CreateMockHttpClientFactory(mockHandler);

        var mockLogger = new Mock<ILogger<ApiClient>>();
        var client = new ApiClient(mockFactory.Object, mockLogger.Object);

        var tempFile = CreateTempFile("test content");
        var job = CreateTestJob(tempFile);

        // Act
        var result = await client.UploadFileAsync(job, tempFile);

        // Assert
        result.Success.Should().BeFalse();
        result.IsRetryable.Should().BeFalse(); // Unexpected exceptions are permanent errors
        result.ErrorMessage.Should().Contain("Unexpected error");
    }

    private Mock<HttpMessageHandler> CreateMockHttpHandler(HttpStatusCode statusCode, string responseContent)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });
        return mockHandler;
    }

    private Mock<IHttpClientFactory> CreateMockHttpClientFactory(Mock<HttpMessageHandler> mockHandler)
    {
        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.example.com")
        };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("RemoteApi")).Returns(httpClient);
        return mockFactory;
    }

    private string CreateTempFile(string content)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.pdf");
        _tempFiles.Add(tempFile);
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private Job CreateTestJob(string filePath)
    {
        return new Job
        {
            JobId = Guid.NewGuid().ToString(),
            SourcePath = "/inbox/test.pdf",
            ProcessingPath = filePath,
            FileSizeBytes = new FileInfo(filePath).Length,
            DiscoveredAtUtc = DateTime.UtcNow,
            Status = JobStatus.Sending,
            AttemptCount = 0
        };
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

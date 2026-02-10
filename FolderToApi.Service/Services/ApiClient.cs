using FolderToApi.Service.Models;
using System.Diagnostics;

namespace FolderToApi.Service.Services;

/// <summary>
/// Client for uploading files to the remote API using multipart/form-data
/// </summary>
public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ApiUploadResult> UploadFileAsync(Job job, string filePath)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Uploading file for job {JobId}: {FileName}",
            job.JobId, Path.GetFileName(filePath));

        try
        {
            // Create HTTP client
            var httpClient = _httpClientFactory.CreateClient("RemoteApi");

            // Build multipart form data
            using var multipartContent = new MultipartFormDataContent();

            // Add file
            var fileInfo = new FileInfo(filePath);
            var fileStream = File.OpenRead(filePath);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            multipartContent.Add(streamContent, "file", fileInfo.Name);

            // Add filename
            multipartContent.Add(new StringContent(fileInfo.Name), "filename");

            // Add metadata
            multipartContent.Add(new StringContent(job.JobId), "job_id");
            multipartContent.Add(new StringContent(DateTime.UtcNow.ToString("O")), "timestamp");
            multipartContent.Add(new StringContent("FolderToApiService"), "source_system");

            if (job.FileSizeBytes.HasValue)
            {
                multipartContent.Add(new StringContent(job.FileSizeBytes.Value.ToString()), "file_size_bytes");
            }

            // Send request
            _logger.LogDebug("Sending POST request for job {JobId} to {Url}",
                job.JobId, httpClient.BaseAddress);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsync("", multipartContent);
            }
            finally
            {
                await fileStream.DisposeAsync();
            }

            stopwatch.Stop();

            // Handle response
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Upload successful for job {JobId}: HTTP {StatusCode} in {Duration}ms",
                    job.JobId,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);

                return ApiUploadResult.Successful(statusCode, stopwatch.Elapsed);
            }

            // Read error response
            var responseBody = await response.Content.ReadAsStringAsync();

            // Determine if error is retryable
            bool isRetryable = IsRetryableStatusCode(statusCode);

            if (isRetryable)
            {
                _logger.LogWarning(
                    "Upload failed with retryable error for job {JobId}: HTTP {StatusCode} in {Duration}ms, Response: {Response}",
                    job.JobId,
                    statusCode,
                    stopwatch.ElapsedMilliseconds,
                    responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody);

                return ApiUploadResult.RetryableError(
                    $"HTTP {statusCode}: {responseBody}",
                    statusCode,
                    stopwatch.Elapsed);
            }
            else
            {
                _logger.LogError(
                    "Upload failed with permanent error for job {JobId}: HTTP {StatusCode} in {Duration}ms, Response: {Response}",
                    job.JobId,
                    statusCode,
                    stopwatch.ElapsedMilliseconds,
                    responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody);

                return ApiUploadResult.PermanentError(
                    $"HTTP {statusCode}: {responseBody}",
                    statusCode,
                    stopwatch.Elapsed);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Network error uploading file for job {JobId} after {Duration}ms",
                job.JobId, stopwatch.ElapsedMilliseconds);

            return ApiUploadResult.RetryableError(
                $"Network error: {ex.Message}",
                responseTime: stopwatch.Elapsed);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Timeout uploading file for job {JobId} after {Duration}ms",
                job.JobId, stopwatch.ElapsedMilliseconds);

            return ApiUploadResult.RetryableError(
                $"Timeout: {ex.Message}",
                responseTime: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error uploading file for job {JobId} after {Duration}ms",
                job.JobId, stopwatch.ElapsedMilliseconds);

            return ApiUploadResult.PermanentError(
                $"Unexpected error: {ex.Message}",
                responseTime: stopwatch.Elapsed);
        }
    }

    private bool IsRetryableStatusCode(int statusCode)
    {
        // Retryable: 408 (Request Timeout), 429 (Too Many Requests), 5xx (Server Errors)
        if (statusCode == 408 || statusCode == 429)
        {
            return true;
        }

        if (statusCode >= 500 && statusCode < 600)
        {
            return true;
        }

        // Non-retryable: most 4xx (client errors)
        return false;
    }
}

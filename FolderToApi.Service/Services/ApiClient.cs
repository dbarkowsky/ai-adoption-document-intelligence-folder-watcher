using FolderToApi.Service.Models;
using FolderToApi.Service.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FolderToApi.Service.Services;

/// <summary>
/// Client for uploading files to the remote API using multipart/form-data
/// </summary>
public class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClient> _logger;
    private readonly ApiClientOptions _options;

    public ApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiClient> logger,
        IOptions<ApiClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
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

            var fileInfo = new FileInfo(filePath);
            var mimeType = GetMimeType(fileInfo.Extension);
            var fileType = GetFileType(mimeType);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var base64File = Convert.ToBase64String(fileBytes);
            var dataUrl = $"data:{mimeType};base64,{base64File}";

            var payload = new
            {
                file = dataUrl,
                file_type = fileType,
                metadata = new
                {
                    size = fileInfo.Length,
                    lastModified = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeMilliseconds()
                },
                model_id = _options.ModelId,
                original_filename = fileInfo.Name,
                title = Path.GetFileNameWithoutExtension(fileInfo.Name),
                workflow_id = _options.WorkflowId
            };
            var jsonPayload = JsonSerializer.Serialize(payload);
            using var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send request
            _logger.LogDebug("Sending POST request for job {JobId} to {Url}",
                job.JobId, httpClient.BaseAddress);

            HttpResponseMessage response;
            response = await httpClient.PostAsync("", requestContent);

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

    private static string GetFileType(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "document";
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}

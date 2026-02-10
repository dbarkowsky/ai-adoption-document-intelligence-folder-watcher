using FolderToApi.Service.Models;

namespace FolderToApi.Service.Services;

/// <summary>
/// Client for uploading files to the remote API
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Uploads a file to the remote API
    /// </summary>
    /// <param name="job">The job containing file information</param>
    /// <param name="filePath">Path to the file to upload</param>
    /// <returns>Upload result</returns>
    Task<ApiUploadResult> UploadFileAsync(Job job, string filePath);
}

/// <summary>
/// Result of an API upload attempt
/// </summary>
public class ApiUploadResult
{
    public bool Success { get; set; }
    public bool IsRetryable { get; set; }
    public int? HttpStatusCode { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiUploadResult Successful(int statusCode, TimeSpan responseTime)
    {
        return new ApiUploadResult
        {
            Success = true,
            IsRetryable = false,
            HttpStatusCode = statusCode,
            ResponseTime = responseTime
        };
    }

    public static ApiUploadResult RetryableError(string errorMessage, int? statusCode = null, TimeSpan? responseTime = null)
    {
        return new ApiUploadResult
        {
            Success = false,
            IsRetryable = true,
            HttpStatusCode = statusCode,
            ResponseTime = responseTime ?? TimeSpan.Zero,
            ErrorMessage = errorMessage
        };
    }

    public static ApiUploadResult PermanentError(string errorMessage, int? statusCode = null, TimeSpan? responseTime = null)
    {
        return new ApiUploadResult
        {
            Success = false,
            IsRetryable = false,
            HttpStatusCode = statusCode,
            ResponseTime = responseTime ?? TimeSpan.Zero,
            ErrorMessage = errorMessage
        };
    }
}

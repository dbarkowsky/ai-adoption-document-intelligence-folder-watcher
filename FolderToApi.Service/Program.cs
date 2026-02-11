using FolderToApi.Service;
using FolderToApi.Service.Configuration;
using FolderToApi.Service.Data;
using FolderToApi.Service.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.IO.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
var loggingConfig = builder.Configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>() ?? new LoggingOptions();

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(Enum.Parse<LogEventLevel>(loggingConfig.MinimumLevel, ignoreCase: true))
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.With<SensitiveDataRedactionEnricher>();

// Console sink
if (loggingConfig.WriteToConsole)
{
    loggerConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
}

// File sink with JSON formatting and rolling
if (loggingConfig.WriteToFile)
{
    // Ensure log directory exists
    Directory.CreateDirectory(loggingConfig.LogDirectory);

    var rollingInterval = Enum.Parse<RollingInterval>(loggingConfig.RollingInterval, ignoreCase: true);

    if (loggingConfig.UseJsonFormatting)
    {
        loggerConfig.WriteTo.File(
            new CompactJsonFormatter(),
            Path.Combine(loggingConfig.LogDirectory, "log-.json"),
            rollingInterval: rollingInterval,
            fileSizeLimitBytes: loggingConfig.FileSizeLimitBytes,
            retainedFileCountLimit: loggingConfig.RetainedFileCountLimit,
            shared: true);
    }
    else
    {
        loggerConfig.WriteTo.File(
            Path.Combine(loggingConfig.LogDirectory, "log-.txt"),
            rollingInterval: rollingInterval,
            fileSizeLimitBytes: loggingConfig.FileSizeLimitBytes,
            retainedFileCountLimit: loggingConfig.RetainedFileCountLimit,
            shared: true,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}");
    }
}

Log.Logger = loggerConfig.CreateLogger();

// Add Serilog to the logging pipeline
builder.Services.AddSerilog();

// Add Windows Event Log (only on Windows)
if (OperatingSystem.IsWindows() && loggingConfig.WriteToEventLog)
{
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "FolderToApiService";
    });
}

try
{
    Log.Information("FolderToApiService starting up");


// Configure Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FolderToApiService";
});

// Configure strongly-typed configuration with validation
builder.Services.AddOptions<FolderWatcherOptions>()
    .Bind(builder.Configuration.GetSection(FolderWatcherOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ApiClientOptions>()
    .Bind(builder.Configuration.GetSection(ApiClientOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RetryPolicyOptions>()
    .Bind(builder.Configuration.GetSection(RetryPolicyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ArchiveRetentionOptions>()
    .Bind(builder.Configuration.GetSection(ArchiveRetentionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<LoggingOptions>()
    .Bind(builder.Configuration.GetSection(LoggingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure HTTP Client Factory
builder.Services.AddHttpClient("RemoteApi", (serviceProvider, client) =>
{
    var apiConfig = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;

    // Set base address
    client.BaseAddress = new Uri(apiConfig.EndpointUrl);

    // Set timeout
    client.Timeout = TimeSpan.FromSeconds(apiConfig.RequestTimeoutSeconds);

    // Add API key header
    var apiKeyProvider = serviceProvider.GetRequiredService<IApiKeyProvider>();
    var apiKey = apiKeyProvider.GetApiKey();
    client.DefaultRequestHeaders.Add(apiConfig.ApiKeyHeaderName, apiKey);

    // Add user agent
    client.DefaultRequestHeaders.Add("User-Agent", "FolderToApiService/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectTimeout = TimeSpan.FromSeconds(10)
    };
});

// Register services
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IFileSystem>(_ => new System.IO.Abstractions.FileSystem());
builder.Services.AddSingleton<IApiKeyProvider, ApiKeyProvider>();
builder.Services.AddSingleton<IStartupInitializationState, StartupInitializationState>();
builder.Services.AddSingleton<IFolderStructureService, FolderStructureService>();
builder.Services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
builder.Services.AddSingleton<IJobRepository, JobRepository>();
builder.Services.AddSingleton<IInboxEnumerator, InboxEnumerator>();
builder.Services.AddSingleton<ICompletenessChecker, CompletenessChecker>();
builder.Services.AddSingleton<IFileClaimer, FileClaimer>();
builder.Services.AddSingleton<IRetryPolicy, RetryPolicy>();
builder.Services.AddSingleton<IApiClient, ApiClient>();

    // Register hosted services (StartupInitializationService runs first)
    builder.Services.AddHostedService<StartupInitializationService>();
    builder.Services.AddHostedService<ScanLoopHostedService>();
    builder.Services.AddHostedService<SendLoopHostedService>();

    var host = builder.Build();

    Log.Information("FolderToApiService started successfully");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FolderToApiService terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("FolderToApiService shutting down");
    Log.CloseAndFlush();
}

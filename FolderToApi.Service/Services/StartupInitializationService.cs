using FolderToApi.Service.Data;

namespace FolderToApi.Service.Services;

/// <summary>
/// Hosted service that performs initialization tasks on startup
/// </summary>
public class StartupInitializationService : BackgroundService
{
    private readonly IFolderStructureService _folderStructureService;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly IJobRepository _jobRepository;
    private readonly IStartupInitializationState _startupState;
    private readonly ILogger<StartupInitializationService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public StartupInitializationService(
        IFolderStructureService folderStructureService,
        IDatabaseInitializer databaseInitializer,
        IJobRepository jobRepository,
        IStartupInitializationState startupState,
        ILogger<StartupInitializationService> logger,
        IHostApplicationLifetime lifetime)
    {
        _folderStructureService = folderStructureService;
        _databaseInitializer = databaseInitializer;
        _jobRepository = jobRepository;
        _startupState = startupState;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Running startup initialization...");

        try
        {
            // Initialize folder structure
            bool folderInitSuccess = await _folderStructureService.InitializeAsync();

            if (!folderInitSuccess)
            {
                _logger.LogCritical("Folder structure initialization failed. Service cannot start.");
                _startupState.MarkFailed();
                _lifetime.StopApplication();
                return;
            }

            // Initialize database
            bool dbInitSuccess = await _databaseInitializer.InitializeAsync();

            if (!dbInitSuccess)
            {
                _logger.LogCritical("Database initialization failed. Service cannot start.");
                _startupState.MarkFailed();
                _lifetime.StopApplication();
                return;
            }

            // Recover orphaned jobs from previous crash/restart
            try
            {
                int recoveredCount = await _jobRepository.RecoverOrphanedSendingJobsAsync();
                if (recoveredCount > 0)
                {
                    _logger.LogInformation("Recovered {Count} orphaned jobs from previous session", recoveredCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover orphaned jobs. Continuing startup.");
                // Don't stop the service for this - just log the error
            }

            _logger.LogInformation("Startup initialization completed successfully");
            _startupState.MarkInitialized();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Startup initialization failed with exception. Service cannot start.");
            _startupState.MarkFailed();
            _lifetime.StopApplication();
        }
    }
}

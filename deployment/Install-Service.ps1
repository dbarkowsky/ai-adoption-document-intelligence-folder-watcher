<#
.SYNOPSIS
    Installs the FolderToApiService as a Windows Service.

.DESCRIPTION
    This script installs the FolderToApiService Windows Service with the following features:
    - Copies executable and configuration files to installation directory
    - Creates Windows Service with automatic startup
    - Configures service recovery options (restart on failure)
    - Validates configuration before starting
    - Sets up appropriate permissions for the service account

.PARAMETER InstallPath
    The directory where the service will be installed. Default: C:\Program Files\FolderToApi

.PARAMETER ServiceName
    The name of the Windows Service. Default: FolderToApiService

.PARAMETER ServiceDisplayName
    The display name shown in Services console. Default: Folder to API Delivery Service

.PARAMETER ServiceDescription
    The description shown in Services console.

.PARAMETER ServiceAccount
    The account under which the service will run. Options:
    - LocalSystem (default)
    - NetworkService
    - LocalService
    - Custom account (e.g., DOMAIN\ServiceAccount)

.PARAMETER ServicePassword
    Password for custom service account. Required if ServiceAccount is a custom account.

.PARAMETER SourcePath
    Path to the published service executable. If not specified, assumes script is run from deployment folder.

.PARAMETER ValidateOnly
    If specified, only validates the configuration without installing the service.

.EXAMPLE
    .\Install-Service.ps1
    Installs the service with default settings using LocalSystem account.

.EXAMPLE
    .\Install-Service.ps1 -ServiceAccount "NetworkService"
    Installs the service to run under NetworkService account.

.EXAMPLE
    .\Install-Service.ps1 -ServiceAccount "DOMAIN\ServiceAccount" -ServicePassword "SecurePass123"
    Installs the service with a custom domain service account.

.EXAMPLE
    .\Install-Service.ps1 -ValidateOnly
    Validates the configuration files without installing the service.

.NOTES
    - This script must be run as Administrator
    - Ensure the executable has been published using the Publish-Service.ps1 script
    - The service account needs Read/Write access to RootPath, database, and log directories
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\FolderToApi",

    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "FolderToApiService",

    [Parameter(Mandatory=$false)]
    [string]$ServiceDisplayName = "Folder to API Delivery Service",

    [Parameter(Mandatory=$false)]
    [string]$ServiceDescription = "Monitors folders for new files and delivers them to a remote API endpoint with retry logic and failure handling.",

    [Parameter(Mandatory=$false)]
    [ValidateSet("LocalSystem", "NetworkService", "LocalService", "Custom")]
    [string]$ServiceAccount = "LocalSystem",

    [Parameter(Mandatory=$false)]
    [string]$CustomServiceAccount,

    [Parameter(Mandatory=$false)]
    [SecureString]$ServicePassword,

    [Parameter(Mandatory=$false)]
    [string]$SourcePath,

    [Parameter(Mandatory=$false)]
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

#region Helper Functions

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-ServiceExists {
    param([string]$Name)
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    return $null -ne $service
}

function Validate-ConfigurationFile {
    param([string]$ConfigPath)

    Write-ColorOutput "Validating configuration file..." "Cyan"

    if (-not (Test-Path $ConfigPath)) {
        throw "Configuration file not found: $ConfigPath"
    }

    try {
        $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json

        # Validate required settings
        $errors = @()

        # Check RootPath
        if ([string]::IsNullOrWhiteSpace($config.FolderWatcher.RootPath)) {
            $errors += "FolderWatcher.RootPath is not configured"
        }

        # Check API Endpoint
        if ([string]::IsNullOrWhiteSpace($config.ApiClient.EndpointUrl)) {
            $errors += "ApiClient.EndpointUrl is not configured"
        }

        # Check API Key (if using Configuration source)
        if ($config.ApiClient.ApiKeySource -eq "Configuration" -and [string]::IsNullOrWhiteSpace($config.ApiClient.ApiKey)) {
            Write-ColorOutput "WARNING: ApiClient.ApiKey is empty. Ensure API key is configured via Windows Credential Manager or update appsettings.json" "Yellow"
        }

        # Check Database connection string
        if ([string]::IsNullOrWhiteSpace($config.Database.ConnectionString)) {
            $errors += "Database.ConnectionString is not configured"
        }

        if ($errors.Count -gt 0) {
            throw "Configuration validation failed:`n" + ($errors -join "`n")
        }

        Write-ColorOutput "✓ Configuration file is valid" "Green"
        return $config
    }
    catch {
        throw "Failed to parse configuration file: $_"
    }
}

function Create-RequiredDirectories {
    param(
        [object]$Config,
        [string]$ServiceAccountName
    )

    Write-ColorOutput "Creating required directories..." "Cyan"

    $directories = @()

    # Log directory
    if (-not [string]::IsNullOrWhiteSpace($Config.Logging.LogDirectory)) {
        $directories += $Config.Logging.LogDirectory
    }

    # Database directory (extract from connection string)
    $dbPath = $Config.Database.ConnectionString -replace "Data Source=", ""
    if (-not [System.IO.Path]::IsPathRooted($dbPath)) {
        $dbPath = Join-Path $InstallPath $dbPath
    }
    $dbDirectory = Split-Path $dbPath -Parent
    if ($dbDirectory) {
        $directories += $dbDirectory
    }

    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            Write-ColorOutput "Creating directory: $dir" "Gray"
            New-Item -Path $dir -ItemType Directory -Force | Out-Null

            # Set permissions for service account
            if ($ServiceAccountName -ne "LocalSystem") {
                Set-DirectoryPermissions -Path $dir -Account $ServiceAccountName
            }
        }
    }

    Write-ColorOutput "✓ Required directories created" "Green"
}

function Set-DirectoryPermissions {
    param(
        [string]$Path,
        [string]$Account
    )

    try {
        $acl = Get-Acl $Path
        $permission = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $Account,
            "Modify",
            "ContainerInherit,ObjectInherit",
            "None",
            "Allow"
        )
        $acl.SetAccessRule($permission)
        Set-Acl $Path $acl
        Write-ColorOutput "  Set permissions for $Account on $Path" "Gray"
    }
    catch {
        Write-ColorOutput "  WARNING: Failed to set permissions on ${Path}: $_" "Yellow"
    }
}

#endregion

#region Main Installation Logic

try {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "FolderToApiService Installation Script" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"

    # Check administrator privileges
    if (-not (Test-Administrator)) {
        throw "This script must be run as Administrator. Please run PowerShell as Administrator and try again."
    }

    # Determine source path
    if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        $SourcePath = Join-Path $PSScriptRoot "publish"
    }

    $executablePath = Join-Path $SourcePath "FolderToApi.Service.exe"
    $configPath = Join-Path $SourcePath "appsettings.json"

    # Validate source files exist
    Write-ColorOutput "Validating source files..." "Cyan"
    if (-not (Test-Path $executablePath)) {
        throw "Executable not found: $executablePath. Please run Publish-Service.ps1 first."
    }
    if (-not (Test-Path $configPath)) {
        throw "Configuration file not found: $configPath"
    }
    Write-ColorOutput "✓ Source files found" "Green"

    # Validate configuration
    $config = Validate-ConfigurationFile -ConfigPath $configPath

    # Exit if validation only
    if ($ValidateOnly) {
        Write-ColorOutput "`n✓ Validation completed successfully. Service was NOT installed." "Green"
        return
    }

    # Check if service already exists
    if (Test-ServiceExists -Name $ServiceName) {
        Write-ColorOutput "Service '$ServiceName' already exists." "Yellow"
        $response = Read-Host "Do you want to uninstall and reinstall? (y/n)"
        if ($response -eq 'y') {
            Write-ColorOutput "Stopping and removing existing service..." "Cyan"
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            sc.exe delete $ServiceName | Out-Null
            Start-Sleep -Seconds 2
            Write-ColorOutput "✓ Existing service removed" "Green"
        }
        else {
            Write-ColorOutput "Installation cancelled." "Yellow"
            return
        }
    }

    # Create installation directory
    Write-ColorOutput "Creating installation directory..." "Cyan"
    if (-not (Test-Path $InstallPath)) {
        New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
    }
    Write-ColorOutput "✓ Installation directory created: $InstallPath" "Green"

    # Copy files
    Write-ColorOutput "Copying service files..." "Cyan"
    $destinationExe = Join-Path $InstallPath "FolderToApi.Service.exe"
    Copy-Item -Path $executablePath -Destination $destinationExe -Force

    # Copy configuration files
    Get-ChildItem -Path $SourcePath -Filter "appsettings*.json" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $InstallPath -Force
        Write-ColorOutput "  Copied: $($_.Name)" "Gray"
    }
    Write-ColorOutput "✓ Service files copied" "Green"

    # Determine service account
    $serviceAccountParam = ""
    switch ($ServiceAccount) {
        "LocalSystem" {
            $serviceAccountParam = "LocalSystem"
            $accountForPermissions = "SYSTEM"
        }
        "NetworkService" {
            $serviceAccountParam = "NT AUTHORITY\NetworkService"
            $accountForPermissions = "NetworkService"
        }
        "LocalService" {
            $serviceAccountParam = "NT AUTHORITY\LocalService"
            $accountForPermissions = "LocalService"
        }
        "Custom" {
            if ([string]::IsNullOrWhiteSpace($CustomServiceAccount)) {
                throw "CustomServiceAccount parameter is required when ServiceAccount is 'Custom'"
            }
            $serviceAccountParam = $CustomServiceAccount
            $accountForPermissions = $CustomServiceAccount
        }
    }

    # Create required directories with permissions
    Create-RequiredDirectories -Config $config -ServiceAccountName $accountForPermissions

    # Create Windows Service
    Write-ColorOutput "Creating Windows Service..." "Cyan"

    $binPath = "`"$destinationExe`""

    if ($ServiceAccount -eq "Custom" -and $ServicePassword) {
        # Create service with custom account
        $passwordPlainText = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ServicePassword)
        )
        sc.exe create $ServiceName binpath= $binPath start= auto obj= $serviceAccountParam password= $passwordPlainText | Out-Null
    }
    else {
        # Create service with built-in account
        if ($serviceAccountParam -eq "LocalSystem") {
            sc.exe create $ServiceName binpath= $binPath start= auto | Out-Null
        }
        else {
            sc.exe create $ServiceName binpath= $binPath start= auto obj= $serviceAccountParam | Out-Null
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create service. Exit code: $LASTEXITCODE"
    }
    Write-ColorOutput "✓ Service created: $ServiceName" "Green"

    # Set service display name and description
    Write-ColorOutput "Configuring service properties..." "Cyan"
    sc.exe config $ServiceName DisplayName= "$ServiceDisplayName" | Out-Null
    sc.exe description $ServiceName "$ServiceDescription" | Out-Null
    Write-ColorOutput "✓ Service properties configured" "Green"

    # Configure service recovery options
    Write-ColorOutput "Configuring service recovery options..." "Cyan"
    # Reset failure count after 24 hours (86400 seconds)
    # Restart after 1 minute on 1st, 2nd, and subsequent failures
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "  WARNING: Failed to configure recovery options" "Yellow"
    }
    else {
        Write-ColorOutput "✓ Recovery options configured (restart on failure)" "Green"
    }

    # Set service to delayed auto-start (reduces startup time impact)
    Write-ColorOutput "Setting delayed auto-start..." "Cyan"
    sc.exe config $ServiceName start= delayed-auto | Out-Null
    Write-ColorOutput "✓ Delayed auto-start configured" "Green"

    # Start the service
    Write-ColorOutput "Starting service..." "Cyan"
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 3

    # Verify service is running
    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq "Running") {
        Write-ColorOutput "✓ Service started successfully" "Green"
    }
    else {
        Write-ColorOutput "WARNING: Service status is $($service.Status)" "Yellow"
    }

    # Final summary
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "Installation Summary" "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput "Service Name:        $ServiceName" "White"
    Write-ColorOutput "Display Name:        $ServiceDisplayName" "White"
    Write-ColorOutput "Installation Path:   $InstallPath" "White"
    Write-ColorOutput "Service Account:     $serviceAccountParam" "White"
    Write-ColorOutput "Status:              $($service.Status)" "White"
    Write-ColorOutput "Startup Type:        Automatic (Delayed)" "White"
    Write-ColorOutput "`n✓ Installation completed successfully!" "Green"
    Write-ColorOutput "`nNext steps:" "Cyan"
    Write-ColorOutput "1. Verify configuration in: $InstallPath\appsettings.json" "White"
    Write-ColorOutput "2. Configure RootPath, API endpoint, and API key" "White"
    Write-ColorOutput "3. Ensure service account has permissions to RootPath and network access" "White"
    Write-ColorOutput "4. Monitor logs in: $($config.Logging.LogDirectory)" "White"
    Write-ColorOutput "5. Check Windows Event Log (Application) for service lifecycle events" "White"
    Write-ColorOutput "`nTo manage the service:" "Cyan"
    Write-ColorOutput "  Start:   Start-Service $ServiceName" "Gray"
    Write-ColorOutput "  Stop:    Stop-Service $ServiceName" "Gray"
    Write-ColorOutput "  Status:  Get-Service $ServiceName" "Gray"
    Write-ColorOutput "  Logs:    Get-EventLog -LogName Application -Source FolderToApiService -Newest 20" "Gray"

}
catch {
    Write-ColorOutput "`n✗ Installation failed: $_" "Red"
    Write-ColorOutput "`nStack trace:" "Red"
    Write-ColorOutput $_.ScriptStackTrace "Red"
    exit 1
}

#endregion

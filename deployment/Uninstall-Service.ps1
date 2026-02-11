<#
.SYNOPSIS
    Uninstalls the FolderToApiService Windows Service.

.DESCRIPTION
    This script safely uninstalls the FolderToApiService Windows Service:
    - Stops the running service
    - Removes the Windows Service registration
    - Optionally removes installation files
    - Optionally removes data (database, logs)

.PARAMETER ServiceName
    The name of the Windows Service to uninstall. Default: FolderToApiService

.PARAMETER InstallPath
    The directory where the service is installed. Default: C:\Program Files\FolderToApi

.PARAMETER RemoveFiles
    If specified, removes the installation directory and all service files.

.PARAMETER RemoveData
    If specified, removes data directories (database, logs). Use with caution!

.PARAMETER Force
    If specified, skips confirmation prompts.

.EXAMPLE
    .\Uninstall-Service.ps1
    Stops and unregisters the service, leaving files in place.

.EXAMPLE
    .\Uninstall-Service.ps1 -RemoveFiles
    Uninstalls the service and removes installation files.

.EXAMPLE
    .\Uninstall-Service.ps1 -RemoveFiles -RemoveData -Force
    Completely removes the service, files, and data without confirmation prompts.

.NOTES
    - This script must be run as Administrator
    - Be careful with -RemoveData as it will delete database and log files
    - The service must be stopped before it can be deleted
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "FolderToApiService",

    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\FolderToApi",

    [Parameter(Mandatory=$false)]
    [switch]$RemoveFiles,

    [Parameter(Mandatory=$false)]
    [switch]$RemoveData,

    [Parameter(Mandatory=$false)]
    [switch]$Force
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

function Get-ConfigurationValue {
    param(
        [string]$ConfigPath,
        [string]$JsonPath
    )

    try {
        if (Test-Path $ConfigPath) {
            $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
            $parts = $JsonPath -split '\.'
            $value = $config
            foreach ($part in $parts) {
                $value = $value.$part
            }
            return $value
        }
    }
    catch {
        return $null
    }
}

#endregion

#region Main Uninstallation Logic

try {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "FolderToApiService Uninstallation Script" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"

    # Check administrator privileges
    if (-not (Test-Administrator)) {
        throw "This script must be run as Administrator. Please run PowerShell as Administrator and try again."
    }

    # Check if service exists
    if (-not (Test-ServiceExists -Name $ServiceName)) {
        Write-ColorOutput "Service '$ServiceName' is not installed." "Yellow"

        if ($RemoveFiles -and (Test-Path $InstallPath)) {
            Write-ColorOutput "Installation directory exists. Proceeding with file removal..." "Yellow"
        }
        else {
            Write-ColorOutput "Nothing to uninstall." "Yellow"
            return
        }
    }
    else {
        # Get service information
        $service = Get-Service -Name $ServiceName
        Write-ColorOutput "Found service: $ServiceName" "White"
        Write-ColorOutput "  Display Name: $($service.DisplayName)" "Gray"
        Write-ColorOutput "  Status:       $($service.Status)" "Gray"

        # Confirm uninstallation
        if (-not $Force) {
            Write-ColorOutput "`nThis will uninstall the service." "Yellow"
            if ($RemoveFiles) {
                Write-ColorOutput "Installation files will be removed from: $InstallPath" "Yellow"
            }
            if ($RemoveData) {
                Write-ColorOutput "WARNING: Database and log files will be DELETED!" "Red"
            }

            $response = Read-Host "`nDo you want to continue? (y/n)"
            if ($response -ne 'y') {
                Write-ColorOutput "Uninstallation cancelled." "Yellow"
                return
            }
        }

        # Stop the service
        Write-ColorOutput "`nStopping service..." "Cyan"
        if ($service.Status -eq "Running") {
            try {
                Stop-Service -Name $ServiceName -Force -ErrorAction Stop
                Write-ColorOutput "[OK] Service stopped" "Green"

                # Wait for service to stop completely
                $timeout = 30
                $elapsed = 0
                while ((Get-Service -Name $ServiceName).Status -ne "Stopped" -and $elapsed -lt $timeout) {
                    Start-Sleep -Seconds 1
                    $elapsed++
                }

                if ((Get-Service -Name $ServiceName).Status -ne "Stopped") {
                    Write-ColorOutput "WARNING: Service did not stop within $timeout seconds" "Yellow"
                }
            }
            catch {
                Write-ColorOutput "WARNING: Failed to stop service: $_" "Yellow"
                Write-ColorOutput "Attempting to delete anyway..." "Yellow"
            }
        }
        else {
            Write-ColorOutput "Service is already stopped" "Gray"
        }

        # Additional wait to ensure service release
        Start-Sleep -Seconds 2

        # Delete the service
        Write-ColorOutput "Removing service registration..." "Cyan"
        $result = sc.exe delete $ServiceName

        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "[OK] Service registration removed" "Green"
        }
        else {
            Write-ColorOutput "WARNING: sc.exe delete returned exit code $LASTEXITCODE" "Yellow"
            Write-ColorOutput "Output: $result" "Yellow"
        }

        # Wait for service deletion to complete
        Start-Sleep -Seconds 2
    }

    # Remove files if requested
    if ($RemoveFiles -and (Test-Path $InstallPath)) {
        Write-ColorOutput "Removing installation files..." "Cyan"

        try {
            # Try to remove the directory
            Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction Stop
            Write-ColorOutput "[OK] Installation files removed from: $InstallPath" "Green"
        }
        catch {
            Write-ColorOutput "WARNING: Failed to remove installation directory: $_" "Yellow"
            Write-ColorOutput "You may need to manually delete: $InstallPath" "Yellow"
        }
    }

    # Remove data if requested
    if ($RemoveData) {
        Write-ColorOutput "Removing data directories..." "Cyan"

        # Get data directories from configuration if possible
        $configPath = Join-Path $InstallPath "appsettings.json"
        $dataDirectories = @()

        # Log directory
        $logDir = Get-ConfigurationValue -ConfigPath $configPath -JsonPath "Logging.LogDirectory"
        if ($logDir -and (Test-Path $logDir)) {
            $dataDirectories += $logDir
        }

        # Database directory (extract from connection string)
        $dbConnectionString = Get-ConfigurationValue -ConfigPath $configPath -JsonPath "Database.ConnectionString"
        if ($dbConnectionString) {
            $dbPath = $dbConnectionString -replace "Data Source=", ""
            if (-not [System.IO.Path]::IsPathRooted($dbPath)) {
                $dbPath = Join-Path $InstallPath $dbPath
            }
            $dbDirectory = Split-Path $dbPath -Parent
            if ($dbDirectory -and (Test-Path $dbDirectory)) {
                $dataDirectories += $dbDirectory
            }
        }

        if ($dataDirectories.Count -eq 0) {
            Write-ColorOutput "No data directories found to remove" "Gray"
        }
        else {
            foreach ($dir in $dataDirectories) {
                try {
                    if (Test-Path $dir) {
                        Write-ColorOutput "  Removing: $dir" "Gray"
                        Remove-Item -Path $dir -Recurse -Force -ErrorAction Stop
                    }
                }
                catch {
                    Write-ColorOutput "  WARNING: Failed to remove ${dir}: $_" "Yellow"
                }
            }
            Write-ColorOutput "[OK] Data directories removed" "Green"
        }
    }

    # Clean up Event Log source (optional)
    Write-ColorOutput "Cleaning up Windows Event Log source..." "Cyan"
    try {
        if ([System.Diagnostics.EventLog]::SourceExists("FolderToApiService")) {
            [System.Diagnostics.EventLog]::DeleteEventSource("FolderToApiService")
            Write-ColorOutput "[OK] Event Log source removed" "Green"
        }
        else {
            Write-ColorOutput "Event Log source not found (may have been already removed)" "Gray"
        }
    }
    catch {
        Write-ColorOutput "WARNING: Failed to remove Event Log source: $_" "Yellow"
    }

    # Final summary
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "Uninstallation Summary" "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput "Service:             Uninstalled" "Green"
    Write-ColorOutput "Installation Files:  $(if ($RemoveFiles) { 'Removed' } else { 'Retained' })" $(if ($RemoveFiles) { "Green" } else { "Yellow" })
    Write-ColorOutput "Data Files:          $(if ($RemoveData) { 'Removed' } else { 'Retained' })" $(if ($RemoveData) { "Green" } else { "Yellow" })

    if (-not $RemoveFiles) {
        Write-ColorOutput "`nInstallation files remain in: $InstallPath" "Yellow"
        Write-ColorOutput "To completely remove, run: .\Uninstall-Service.ps1 -RemoveFiles" "Gray"
    }

    Write-ColorOutput "`n[OK] Uninstallation completed successfully!" "Green"

}
catch {
    Write-ColorOutput ("Uninstallation failed: " + $_) -Color Red
    Write-ColorOutput "Stack trace:" -Color Red
    Write-ColorOutput $_.ScriptStackTrace -Color Red
    exit 1
}

#endregion

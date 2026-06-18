<#
.SYNOPSIS
    Publishes the FolderToApiService as a single-file executable.

.DESCRIPTION
    This script builds and publishes the FolderToApiService application as a self-contained,
    single-file executable ready for deployment to Windows Server.

    Features:
    - Builds in Release configuration
    - Creates a single-file executable with all dependencies embedded
    - Self-contained deployment (no .NET runtime installation required)
    - Includes all configuration files
    - Ready-to-deploy output in the publish folder

.PARAMETER Configuration
    The build configuration. Default: Release

.PARAMETER Runtime
    The target runtime identifier. Default: win-x64
    Options: win-x64, win-x86, win-arm64

.PARAMETER OutputPath
    The output directory for the published files. Default: .\publish

.PARAMETER ProjectPath
    Path to the .csproj file. If not specified, searches for FolderToApi.Service.csproj

.PARAMETER IncludeSymbols
    If specified, includes PDB symbol files for debugging.

.PARAMETER NoSelfContained
    If specified, publishes as framework-dependent (requires .NET runtime on target).

.EXAMPLE
    .\Publish-Service.ps1
    Publishes the service with default settings (Release, win-x64, single-file, self-contained).

.EXAMPLE
    .\Publish-Service.ps1 -Runtime win-x64 -IncludeSymbols
    Publishes with debug symbols included.

.EXAMPLE
    .\Publish-Service.ps1 -OutputPath "C:\Deploy\FolderToApi"
    Publishes to a custom output directory.

.NOTES
    - Requires .NET 10.0 SDK or later
    - The published output will be in the specified OutputPath
    - After publishing, use Install-Service.ps1 to install the service
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "publish",

    [Parameter(Mandatory=$false)]
    [string]$ProjectPath,

    [Parameter(Mandatory=$false)]
    [switch]$IncludeSymbols,

    [Parameter(Mandatory=$false)]
    [switch]$NoSelfContained
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

function Test-DotNetInstalled {
    try {
        $dotnetVersion = dotnet --version
        return $true
    }
    catch {
        return $false
    }
}

function Get-DotNetSdkVersion {
    try {
        $output = dotnet --list-sdks
        $versions = $output | ForEach-Object {
            if ($_ -match '^(\d+\.\d+\.\d+)') {
                [version]$matches[1]
            }
        }
        return $versions | Sort-Object -Descending | Select-Object -First 1
    }
    catch {
        return $null
    }
}

#endregion

#region Main Publish Logic

try {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "FolderToApiService Publish Script" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"

    # Check for .NET SDK
    Write-ColorOutput "Checking .NET SDK installation..." "Cyan"
    if (-not (Test-DotNetInstalled)) {
        throw ".NET SDK is not installed. Please install .NET 10.0 SDK or later from https://dot.net"
    }

    $sdkVersion = Get-DotNetSdkVersion
    Write-ColorOutput "[OK] .NET SDK version: $sdkVersion" "Green"

    # Check minimum version (10.0)
    if ($sdkVersion -lt [version]"10.0.0") {
        throw ".NET SDK 10.0 or later is required. Current version: $sdkVersion"
    }

    # Locate project file
    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        Write-ColorOutput "Locating project file..." "Cyan"

        # Search in common locations
        $searchPaths = @(
            (Join-Path $PSScriptRoot "..\FolderToApi.Service\FolderToApi.Service.csproj"),
            (Join-Path (Get-Location) "FolderToApi.Service\FolderToApi.Service.csproj"),
            (Join-Path (Get-Location) "FolderToApi.Service.csproj")
        )

        foreach ($path in $searchPaths) {
            if (Test-Path $path) {
                $ProjectPath = $path
                break
            }
        }

        if (-not $ProjectPath) {
            throw "Could not locate FolderToApi.Service.csproj. Please specify -ProjectPath parameter."
        }
    }

    if (-not (Test-Path $ProjectPath)) {
        throw "Project file not found: $ProjectPath"
    }

    $ProjectPath = Resolve-Path $ProjectPath
    Write-ColorOutput "[OK] Project file: $ProjectPath" "Green"

    # Resolve output path
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path $PSScriptRoot $OutputPath
    }

    # Create output directory
    Write-ColorOutput "Preparing output directory..." "Cyan"
    if (Test-Path $OutputPath) {
        Write-ColorOutput "  Cleaning existing output directory..." "Gray"
        Remove-Item -Path $OutputPath -Recurse -Force
    }
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
    Write-ColorOutput "[OK] Output directory: $OutputPath" "Green"

    # Build publish arguments
    $publishArgs = @(
        "publish",
        "`"$ProjectPath`"",
        "-c", $Configuration,
        "-r", $Runtime,
        "-o", "`"$OutputPath`"",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true"
    )

    # Self-contained or framework-dependent
    if ($NoSelfContained) {
        $publishArgs += "--self-contained", "false"
        Write-ColorOutput "Publishing as framework-dependent (requires .NET runtime on target)" "Yellow"
    }
    else {
        $publishArgs += "--self-contained", "true"
        Write-ColorOutput "Publishing as self-contained (includes .NET runtime)" "White"
    }

    # Include or exclude symbols
    if (-not $IncludeSymbols) {
        $publishArgs += "-p:DebugType=None"
        $publishArgs += "-p:DebugSymbols=false"
    }

    # Publish the application
    Write-ColorOutput "`nPublishing application..." "Cyan"
    Write-ColorOutput "Configuration:  $Configuration" "White"
    Write-ColorOutput "Runtime:        $Runtime" "White"
    Write-ColorOutput "Output:         $OutputPath" "White"
    Write-ColorOutput ""

    $publishCommand = "dotnet $($publishArgs -join ' ')"
    Write-ColorOutput "Running: dotnet publish..." "Gray"

    # Execute publish command
    $process = Start-Process -FilePath "dotnet" -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru

    if ($process.ExitCode -ne 0) {
        throw "Publish failed with exit code $($process.ExitCode)"
    }

    Write-ColorOutput "[OK] Application published successfully" "Green"

    # Verify output files
    Write-ColorOutput "`nVerifying published files..." "Cyan"
    $executablePath = Join-Path $OutputPath "FolderToApi.Service.exe"
    $configPath = Join-Path $OutputPath "appsettings.json"

    if (-not (Test-Path $executablePath)) {
        throw "Executable not found in output: $executablePath"
    }

    if (-not (Test-Path $configPath)) {
        Write-ColorOutput "WARNING: appsettings.json not found. Copying from project..." "Yellow"
        $projectDir = Split-Path $ProjectPath -Parent
        $sourceConfig = Join-Path $projectDir "appsettings.json"
        if (Test-Path $sourceConfig) {
            Copy-Item -Path $sourceConfig -Destination $OutputPath
            Write-ColorOutput "[OK] Configuration file copied" "Green"
        }
        else {
            Write-ColorOutput "WARNING: Could not find appsettings.json to copy" "Yellow"
        }
    }

    # Get executable size
    $exeSize = (Get-Item $executablePath).Length
    $exeSizeMB = [math]::Round($exeSize / 1MB, 2)

    Write-ColorOutput "[OK] Executable found: FolderToApi.Service.exe ($exeSizeMB MB)" "Green"

    # List all files in output
    Write-ColorOutput "`nPublished files:" "Cyan"
    Get-ChildItem -Path $OutputPath | ForEach-Object {
        $size = [math]::Round($_.Length / 1KB, 2)
        Write-ColorOutput "  $($_.Name) ($size KB)" "Gray"
    }

    # Final summary
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "Publish Summary" "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput "Configuration:      $Configuration" "White"
    Write-ColorOutput "Runtime:            $Runtime" "White"
    Write-ColorOutput "Deployment Type:    $(if ($NoSelfContained) { 'Framework-dependent' } else { 'Self-contained' })" "White"
    Write-ColorOutput "Single File:        Yes" "White"
    Write-ColorOutput "Symbols Included:   $(if ($IncludeSymbols) { 'Yes' } else { 'No' })" "White"
    Write-ColorOutput "Executable Size:    $exeSizeMB MB" "White"
    Write-ColorOutput "Output Location:    $OutputPath" "White"
    Write-ColorOutput "`n[OK] Publish completed successfully!" "Green"
    Write-ColorOutput "`nNext steps:" "Cyan"
    Write-ColorOutput "1. Review and update configuration files in the output directory" "White"
    Write-ColorOutput "2. Test the executable locally if possible: .\publish\FolderToApi.Service.exe" "White"
    Write-ColorOutput "3. Copy the publish folder to the target Windows Server" "White"
    Write-ColorOutput "4. Run the installation script: .\Install-Service.ps1" "White"
    Write-ColorOutput "`nInstallation command:" "Cyan"
    Write-ColorOutput "  .\Install-Service.ps1 -SourcePath `"$OutputPath`"" "Gray"

}
catch {
    Write-ColorOutput "`n[FAIL] Publish failed: $_" "Red"
    Write-ColorOutput "`nStack trace:" "Red"
    Write-ColorOutput $_.ScriptStackTrace "Red"
    exit 1
}

#endregion

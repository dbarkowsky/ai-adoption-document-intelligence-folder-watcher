# PowerShell script to encrypt an API key using Windows DPAPI
# Usage: .\EncryptApiKey.ps1 -ApiKey "your-api-key-here"
#
# This script must be run on the target Windows Server with the same service account
# that will run the FolderToApiService

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey
)

Add-Type -AssemblyName System.Security

$plainTextBytes = [System.Text.Encoding]::UTF8.GetBytes($ApiKey)
$encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
    $plainTextBytes,
    $null,
    [System.Security.Cryptography.DataProtectionScope]::LocalMachine
)
$encryptedBase64 = [Convert]::ToBase64String($encryptedBytes)

Write-Host ""
Write-Host "API Key encrypted successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Add this to your appsettings.Production.json:" -ForegroundColor Yellow
Write-Host ""
Write-Host '"ApiClient": {' -ForegroundColor Cyan
Write-Host '  "ApiKeySource": "DPAPI",' -ForegroundColor Cyan
Write-Host "  `"ApiKey`": `"$encryptedBase64`"" -ForegroundColor Cyan
Write-Host '}' -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT: This encrypted value can only be decrypted on this machine by the LocalMachine scope." -ForegroundColor Red
Write-Host ""

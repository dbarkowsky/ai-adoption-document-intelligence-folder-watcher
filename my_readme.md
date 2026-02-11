To build on wsl:

   ./deployment/publish-service.sh

To uninstall:
.\Uninstall-Service.ps1 -RemoveFiles -ServiceName "FolderToApiService"

To install:

.\Install-Service.ps1 -SourcePath ".\publish"

More info:

# Service config (path, account, start mode)
Get-CimInstance Win32_Service -Filter "Name='FolderToApiService'" |
  Select Name, State, StartMode, StartName, PathName

sc.exe qc FolderToApiService

# SCM errors around the attempt (last 10 minutes)
Get-WinEvent -FilterHashtable @{
  LogName='System'
  ProviderName='Service Control Manager'
  StartTime=(Get-Date).AddMinutes(-10)
} | Select TimeCreated, Id, Message | Format-List
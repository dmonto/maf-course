[CmdletBinding()]
param(
    [ValidateSet('fixture','azure')][string]$Mode = 'fixture',
    [string]$Url = 'http://127.0.0.1:8082',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $unitRoot 'Shared/Unit16.EvidenceHost/Unit16.EvidenceHost.csproj'
$previousMode = [Environment]::GetEnvironmentVariable('Runner__Mode')
try {
    $env:Runner__Mode = $Mode
    if (-not $NoBuild) {
        Write-Host ('[COMMAND] dotnet build "{0}" -p:RestoreLockedMode=true -m:1' -f $project)
        & dotnet build $project '-p:RestoreLockedMode=true' '-m:1'
        if ($LASTEXITCODE -ne 0) { throw 'Host build failed.' }
    }
    Write-Host ('[MILESTONE] Start the local host: mode={0}; URL={1}' -f $Mode, $Url)
    Write-Host ('[COMMAND] dotnet run --project "{0}" --no-build --no-launch-profile -- --urls {1}' -f $project, $Url)
    Write-Host '[NEXT] In terminal B, run Test-Http.ps1 or Run-Example.ps1 -Example v07 -Http.'
    & dotnet run --project $project --no-build --no-launch-profile -- --urls $Url
    if ($LASTEXITCODE -ne 0) { throw 'The evidence host exited with an error.' }
} finally {
    [Environment]::SetEnvironmentVariable('Runner__Mode', $previousMode)
}

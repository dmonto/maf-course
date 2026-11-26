[CmdletBinding()]
param(
    [ValidateSet('approved','expired','budget')][string]$Outcome = 'approved',
    [ValidateRange(1,10000)][int]$StatusMs = 1000,
    [ValidateRange(1,10000)][int]$PolicyMs = 1500
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SkConcurrentMigration.csproj'
Write-Host ('[COMMAND] dotnet restore "{0}" --locked-mode' -f $project)
& dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
Write-Host ('[COMMAND] dotnet run --project "{0}" --no-restore -- {1} {2} {3}' -f $project, $Outcome, $StatusMs, $PolicyMs)
& dotnet run --project $project --no-restore -- $Outcome $StatusMs $PolicyMs
if ($LASTEXITCODE -ne 0) { throw 'The source workflow did not satisfy its checks.' }

[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    dotnet build (Join-Path $unitRoot 'Maf.Unit11.slnx') -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
foreach ($example in 'v01','v02','v04','v05','v06','v07') {
    Write-Host "Checking $example in demo mode."
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $example -Demo -NoBuild
}

dotnet run --no-build --project (Join-Path $unitRoot 'Tests/Unit11.Checks/Unit11.Checks.csproj') -- --demo
if ($LASTEXITCODE -ne 0) { throw 'The HTTP or durable processing checks failed.' }
Write-Host 'All local examples checks passed. No Azure or Service Bus calls were made.'

param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Helpers.ps1')
$root = Get-Unit15Root
if (-not $NoBuild) {
    Write-Host '[MILESTONE] Build the solution.'
    Write-Host ('[COMMAND] dotnet build "{0}" -m:1' -f (Join-Path $root 'Maf.Unit15.slnx'))
    & dotnet build (Join-Path $root 'Maf.Unit15.slnx') -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Unit 15 build failed.' }
}
Write-Host '[MILESTONE] Run the verification project: every example and exercise is executed and its RESULT line checked.'
Invoke-Unit15Project -Project 'Verification/Verification.csproj' -NoBuild

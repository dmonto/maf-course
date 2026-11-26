#requires -Version 5.1
param([switch]$NoBuild)
$ErrorActionPreference='Stop'
$unit14Root=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1')
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
if (-not $NoBuild) {
    Write-Host '[MILESTONE] Restore and build the pinned .NET solution.'
    Write-Host ('[COMMAND] dotnet restore "{0}" --locked-mode -m:1' -f (Join-Path $unit14Root 'Maf.Unit14.slnx'))
    & dotnet restore (Join-Path $unit14Root 'Maf.Unit14.slnx') --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    Write-Host ('[COMMAND] dotnet build "{0}" --no-restore -m:1' -f (Join-Path $unit14Root 'Maf.Unit14.slnx'))
    & dotnet build (Join-Path $unit14Root 'Maf.Unit14.slnx') --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
Write-Host '[MILESTONE] Verify shell compatibility.'
& (Join-Path $unit14Root 'Verification/Test-ShellCompatibility.ps1')
Write-Host '[MILESTONE] Run every example in fixture mode.'
$unit14Available=Get-Content (Join-Path $unit14Root 'examples.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($unit14Id in @('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02','checks')) {
    # The student archive omits the instructor-only exercise-resolution projects.
    if ($unit14Id -in @('ex01','ex02') -and -not ($unit14Available | Where-Object id -eq $unit14Id)) { continue }
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $unit14Id -Mode fixture -NoBuild
}
Write-Host '[MILESTONE] Verify the deployment, Podman and session scripts.'
& (Join-Path $unit14Root 'Verification/Test-DeploymentScripts.ps1')
& (Join-Path $unit14Root 'Verification/Test-PodmanScripts.ps1')
& (Join-Path $unit14Root 'Verification/Test-SessionDeployment.ps1')
Write-Host '[CONCLUSION] ALL UNIT 14 LOCAL CHECKS PASSED'

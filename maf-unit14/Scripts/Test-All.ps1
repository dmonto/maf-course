#requires -Version 5.1
param([switch]$NoBuild)
$ErrorActionPreference='Stop'
$unit14Root=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1')
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
if (-not $NoBuild) {
    & dotnet restore (Join-Path $unit14Root 'Maf.Unit14.slnx') --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & dotnet build (Join-Path $unit14Root 'Maf.Unit14.slnx') --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
& (Join-Path $unit14Root 'Verification/Test-ShellCompatibility.ps1')
$unit14Python=if ($env:PYTHON_COMMAND) { $env:PYTHON_COMMAND } else { 'python' }
& $unit14Python (Join-Path $unit14Root 'Verification/test_probe.py')
if ($LASTEXITCODE -ne 0) { throw 'Probe diagnostic checks failed.' }
foreach ($unit14Id in @('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02','checks')) {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $unit14Id -Mode fixture -NoBuild
}
& (Join-Path $unit14Root 'Verification/Test-DeploymentScripts.ps1')
& (Join-Path $unit14Root 'Verification/Test-PodmanScripts.ps1')
Write-Host 'ALL UNIT 14 LOCAL CHECKS PASSED'

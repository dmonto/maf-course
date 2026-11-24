[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit13Root = Split-Path -Parent $PSScriptRoot
if (-not $NoBuild) {
    & dotnet restore (Join-Path $unit13Root 'Maf.Unit13.slnx') --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & dotnet build (Join-Path $unit13Root 'Maf.Unit13.slnx') --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
foreach ($unit13Id in @('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02','checks')) {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $unit13Id -Mode fixture -NoBuild
    if ($LASTEXITCODE -ne 0) { throw "Unexpected exit code for $unit13Id." }
    Write-Output "PASS $unit13Id"
}
Write-Output 'ALL UNIT 13 LOCAL CHECKS PASSED'

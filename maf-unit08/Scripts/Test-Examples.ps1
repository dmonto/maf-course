param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit08Root = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    & dotnet restore (Join-Path $unit08Root 'Maf.Unit08.slnx') --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build (Join-Path $unit08Root 'Maf.Unit08.slnx') --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
foreach ($id in 'v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02') {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $id -Demo -NoBuild
}
& dotnet run --no-build --project (Join-Path $unit08Root 'Verification/Unit08.Checks.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Contract and concurrency checks failed.' }
Write-Host 'PASS: all example entry points, and the deeper checks.'

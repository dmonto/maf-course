param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$unit03PackageRoot = Split-Path $PSScriptRoot -Parent
$unit03Solution = Join-Path $unit03PackageRoot 'Maf.Unit03.slnx'
if (-not $SkipBuild) {
    & dotnet restore $unit03Solution --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    & dotnet build $unit03Solution --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
& dotnet run --project (Join-Path $unit03PackageRoot 'Verification/Unit03.Checks.csproj') --no-build
if ($LASTEXITCODE -ne 0) { throw 'Offline verification failed.' }
foreach ($unit03ExampleId in @('v01','v02','v03','v04','v05','v06','v07','v08','ex01','ex02')) {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $unit03ExampleId -Demo -NoBuild
}
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example v05 -Demo -Case alternate -NoBuild
Write-Host 'PASS: verification, eight examples, both exercise solutions and alternate injection completed without Azure inference.'

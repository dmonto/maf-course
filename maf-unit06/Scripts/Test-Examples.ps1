param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$unit06Root = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    & dotnet build (Join-Path $unit06Root 'Maf.Unit06.slnx') '-m:1'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
foreach ($id in @('v01', 'v02', 'v03', 'v04', 'v06', 'v07')) {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $id -Demo -NoBuild -Session "$id-$suffix"
}
$session = 'replica-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example v05 -Demo -NoBuild -Session $session -Replica A
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example v05 -Demo -NoBuild -Session $session -Replica B
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example ex01 -Demo -NoBuild -Session "ex01-$suffix" -ConfirmOS 'Windows 11'
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example ex02 -Demo -NoBuild -VerifyExpiry
& (Join-Path $PSScriptRoot 'Test-Memory.ps1')
Write-Host 'PASS: all example projects and both exercise projects executed in explicit demo mode.'

[CmdletBinding()]
param([switch]$NoBuild, [switch]$IncludeSolutions)
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
foreach ($number in 1..10) {
    $example = 'v{0:d2}' -f $number
    Write-Host ('[MILESTONE] Verify ' + $example)
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example $example -NoBuild
}
$report = Join-Path $unitRoot 'artifacts/verified-regression.json'
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example v07 -NoBuild -InjectRegression -ExpectedExitCode 1 -Report $report
$result = Get-Content $report -Raw | ConvertFrom-Json
if ($result.count -ne 4 -or $result.failures -ne 2) { throw 'Expected exactly two failed VPN cases in the regression report.' }
Write-Host '[CHECK] PASS | The regression was detected in exactly the two VPN cases.'
if ($IncludeSolutions) {
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example ex01 -NoBuild
    & (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example ex02 -NoBuild
}
Write-Host '[CONCLUSION] All ten local C# examples and the expected regression gate passed. No Azure inference was requested.'

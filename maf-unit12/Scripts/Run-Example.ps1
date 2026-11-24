[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08','v09','ex01','ex02')][string]$Example,
    [ValidateSet('fixture','fixture-bad','azure')][string]$Mode = 'fixture',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string]$Baseline,
    [string]$Candidate,
    [string]$Report,
    [ValidateSet('bad','fixed')][string]$Variant
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unitRoot = Split-Path -Parent $PSScriptRoot
$searchRoot = $unitRoot 
$folders = @(Get-ChildItem -LiteralPath $searchRoot -Directory | Where-Object Name -Like "$Example-*")
if ($folders.Count -ne 1) { throw "Project directory not found for $Example." }
$projects = @(Get-ChildItem -LiteralPath $folders[0].FullName -Filter '*.csproj')
if ($projects.Count -ne 1) { throw "Expected one project for $Example." }
if (($Baseline -or $Candidate) -and $Example -notin @('v04','v06')) { throw 'Report pairs are supported by v04 and v06.' }
if (($Baseline -and -not $Candidate) -or ($Candidate -and -not $Baseline)) { throw 'Supply both report paths.' }
if ($Report -and $Example -ne 'v09') { throw '-Report is supported by v09.' }
$runArgs = @('run', '--project', $projects[0].FullName, '--configuration', $Configuration)
if ($NoBuild) { $runArgs += '--no-build' }
$runArgs += @('--', $Mode)
if ($Baseline) { $runArgs += @('--baseline', (Resolve-Path -LiteralPath $Baseline).Path) }
if ($Candidate) { $runArgs += @('--candidate', (Resolve-Path -LiteralPath $Candidate).Path) }
if ($Report) { $runArgs += @('--report', (Resolve-Path -LiteralPath $Report).Path) }
if ($Variant) { $runArgs += @('--variant', $Variant) }
& dotnet @runArgs
# Preserve expected rejection (1) and inconclusive execution (2) for callers and CI.
$runExit = $LASTEXITCODE
exit $runExit

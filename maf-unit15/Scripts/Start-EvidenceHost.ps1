param([ValidateSet('fixture','azure')][string]$Mode = 'fixture', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Helpers.ps1')
Write-Host "[MILESTONE] Start the local evidence host in $Mode mode (Ctrl+C to stop). Probe it with Run-Example.ps1 -Mode http."
Invoke-Unit15Project -Project 'EvidenceHost/Maf.Unit15.EvidenceHost.csproj' -ApplicationArguments @('--mode', $Mode) -NoBuild:$NoBuild

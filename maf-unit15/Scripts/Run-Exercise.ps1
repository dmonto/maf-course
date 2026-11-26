param([Parameter(Mandatory=$true)][ValidateSet('01','02')][string]$Exercise, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Helpers.ps1')
$projects = @{'01'='Exercises/Ex01.TransactionBoundaries/Ex01.TransactionBoundaries.csproj'; '02'='Exercises/Ex02.ReportingConsumer/Ex02.ReportingConsumer.csproj'}
Write-Host "[START] Exercise $Exercise (fixture data)"
Invoke-Unit15Project -Project $projects[$Exercise] -ApplicationArguments @('--mode','fixture') -NoBuild:$NoBuild

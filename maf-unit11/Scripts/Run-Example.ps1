[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('v01','v02','v03','v04','v05','v06','v07')][string]$Example,
    [switch]$Demo,
    [switch]$Live,
    [switch]$NoBuild,
    [string]$Command = '',
    [int]$Port = -1,
    [string]$File = '',
    [string]$Database = '',
    [int]$Count = 0,
    [int]$DelayMs = 0
)
$ErrorActionPreference = 'Stop'
if ($Demo -and $Live) { throw 'Choose -Demo or -Live.' }
$unitRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Import-Env.ps1')
. (Join-Path $PSScriptRoot 'Invoke-UnitProject.ps1')
$projects = @{
    v01 = 'v01-integration-with-queues/Unit11.V01.csproj'
    v02 = 'v02-real-time-streaming/Unit11.V02.csproj'
    v03 = 'v03-external-events/Unit11.V03.csproj'
    v04 = 'v04-asynchronous-retries/Unit11.V04.csproj'
    v05 = 'v05-concurrency/Unit11.V05.csproj'
    v06 = 'v06-event-persistence/Unit11.V06.csproj'
    v07 = 'v07-enterprise-events/Unit11.V07.csproj'
}
$applicationArguments = [System.Collections.Generic.List[string]]::new()
if ($Demo) { $applicationArguments.Add('--demo') }
if ($Live) { $applicationArguments.Add('--live') }
if ($Command) { $applicationArguments.Add('--command'); $applicationArguments.Add($Command) }
if ($Port -ge 0) { $applicationArguments.Add('--port'); $applicationArguments.Add([string]$Port) }
if ($File) { $applicationArguments.Add('--file'); $applicationArguments.Add((Resolve-Path -LiteralPath $File).Path) }
if ($Database) { $applicationArguments.Add('--database'); $applicationArguments.Add([System.IO.Path]::GetFullPath($Database)) }
if ($Count -gt 0) { $applicationArguments.Add('--count'); $applicationArguments.Add([string]$Count) }
if ($DelayMs -gt 0) { $applicationArguments.Add('--delay-ms'); $applicationArguments.Add([string]$DelayMs) }
Invoke-UnitProject -Project (Join-Path $unitRoot $projects[$Example]) `
    -ApplicationArguments $applicationArguments.ToArray() -NoBuild:$NoBuild

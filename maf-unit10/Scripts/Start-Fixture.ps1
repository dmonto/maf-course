param(
    [ValidateSet('none', 'stale', 'invalid', 'tenant', 'forbidden', 'unavailable')]
    [string]$StatusFault = 'none',
    [ValidateSet('none', 'scope', 'rate')]
    [string]$SlackFault = 'none',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Import-Env.ps1')
$unit10FixtureRoot = Split-Path $PSScriptRoot -Parent
$env:FIXTURE_STATUS_FAULT = $StatusFault
$env:FIXTURE_SLACK_FAULT = $SlackFault
$fixtureArguments = @('run', '--project', (Join-Path $unit10FixtureRoot 'v01-rest-apis/Unit10.V01.csproj'))
if ($NoBuild) { $fixtureArguments += '--no-build' }
$fixtureArguments += @('--', 'fixture')
& dotnet @fixtureArguments
if ($LASTEXITCODE -ne 0) { throw 'The local fixture failed.' }

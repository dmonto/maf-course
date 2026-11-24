param(
    [string]$Example = 'v01',
    [switch]$Demo,
    [switch]$Live,
    [switch]$NoBuild,
    [string]$Profile,
    [string]$Service,
    [string]$Case,
    [string]$Operation,
    [switch]$Send,
    [switch]$Approve,
    [switch]$Agent
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($Demo -and $Live) { throw 'Choose -Demo or -Live.' }
if ($Approve -and $Live) { throw '-Approve is available only for the local demo.' }
if ($Approve -and -not $Send) { throw 'Use -Send with the local demo -Approve switch.' }
$unit10Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit10Root 'manifest.json') -Raw | ConvertFrom-Json
$items = @($manifest.examples) + @($manifest.verification)
$match = @($items | Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Choose v01 through v09, ex01, ex02 or verify.' }
$runArguments = @('run', '--project', (Join-Path $unit10Root $match[0].project))
if ($NoBuild) { $runArguments += '--no-build' }
$runArguments += '--'
$runArguments += $(if ($Live) { '--live' } else { '--demo' })
foreach ($entry in @{ '--profile'=$Profile; '--service'=$Service; '--case'=$Case; '--operation'=$Operation }.GetEnumerator()) {
    if ($entry.Value) { $runArguments += @($entry.Key, $entry.Value) }
}
if ($Send) { $runArguments += '--send' }
if ($Approve) { $runArguments += '--approve' }
if ($Agent) { $runArguments += '--agent' }
& dotnet @runArguments
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed. Inspect its RESULT output above." }

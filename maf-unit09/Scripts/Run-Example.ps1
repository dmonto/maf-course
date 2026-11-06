param(
    [string]$Example = 'v01',
    [switch]$Demo,
    [switch]$Live,
    [switch]$NoBuild,
    [string]$Profile,
    [string]$Question,
    [ValidateSet('keyword','vector','hybrid')][string]$Mode,
    [string]$Case,
    [string]$Service,
    [string]$Version,
    [switch]$ForceFailure
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($Demo -and $Live) { throw 'Choose -Demo or -Live.' }
$unit09Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit09Root 'manifest.json') -Raw | ConvertFrom-Json
$items = @($manifest.examples) + @($manifest.exercises) + @($manifest.verification)
$match = @($items | Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Choose v01 through v10, ex01, ex02 or verify.' }
if ($Live -and $Example -eq 'verify') { throw 'Verification uses local dependency doubles.' }
$runArgs = @('run', '--project', (Join-Path $unit09Root $match[0].project))
if ($NoBuild) { $runArgs += '--no-build' }
$runArgs += '--'
$runArgs += $(if ($Live) { '--live' } else { '--demo' })
foreach ($entry in @{ '--profile'=$Profile; '--question'=$Question; '--mode'=$Mode;
    '--case'=$Case; '--service'=$Service; '--version'=$Version }.GetEnumerator()) {
    if ($entry.Value) { $runArgs += @($entry.Key, $entry.Value) }
}
if ($ForceFailure) {
    if ($Example -ne 'ex02') { throw '-ForceFailure belongs to ex02.' }
    $runArgs += '--force-failure'
}
& dotnet @runArgs
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed. Inspect its output above." }

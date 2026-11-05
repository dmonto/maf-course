param(
    [string]$Example = 'v04',
    [ValidateSet('run','pause','resume')][string]$Command,
    [string]$Service,
    [ValidateSet('yes','no')][string]$Decision,
    [string]$State,
    [switch]$Demo,
    [switch]$Live,
    [switch]$Fail,
    [switch]$Serial,
    [switch]$Reset,
    [switch]$Stale,
    [switch]$FailAfterSave,
    [Nullable[int]]$StatusDelayMs,
    [Nullable[int]]$GuideDelayMs,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
$unit07Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit07Root 'manifest.json') -Raw | ConvertFrom-Json
$match = @((@($manifest.examples) + @($manifest.exercises)) |
    Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Use v01 through v09, ex01 or ex02.' }
$id = $match[0].id
if ($Stale -and $id -ne 'ex01') { throw '-Stale belongs to ex01.' }
if ($FailAfterSave -and $id -ne 'ex02') { throw '-FailAfterSave belongs to ex02.' }
$project = Join-Path $unit07Root $match[0].project
$forwardArgs = @('run', '--project', $project)
if ($NoBuild) { $forwardArgs += '--no-build' }
$forwardArgs += '--'
if ($Command) { $forwardArgs += $Command }
elseif ($Service) { $forwardArgs += $(if ($id -eq 'v08') { 'pause' } else { 'run' }) }
if ($Service) { $forwardArgs += $Service }
if ($Decision) { $forwardArgs += @('--decision', $Decision) }
if ($State) { $forwardArgs += @('--state', $State) }
if ($Demo) { $forwardArgs += '--demo' }
if ($Live) { $forwardArgs += '--live' }
if ($Fail) { $forwardArgs += '--fail' }
if ($Serial) { $forwardArgs += '--serial' }
if ($Reset) { $forwardArgs += '--reset' }
if ($Stale) { $forwardArgs += '--stale' }
if ($FailAfterSave) { $forwardArgs += '--fail-after-save' }
if ($null -ne $StatusDelayMs) { $forwardArgs += @('--status-delay-ms', [string]$StatusDelayMs) }
if ($null -ne $GuideDelayMs) { $forwardArgs += @('--guide-delay-ms', [string]$GuideDelayMs) }
& dotnet @forwardArgs
if ($LASTEXITCODE -ne 0) { throw "Example $id failed. Inspect its failure JSON." }

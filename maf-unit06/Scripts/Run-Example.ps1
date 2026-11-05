param(
    [string]$Example = 'v04',
    [switch]$Demo,
    [switch]$Live,
    [string]$Session,
    [string]$InputText,
    [string]$Replica,
    [switch]$Audit,
    [string]$ConfirmOS,
    [switch]$VerifyExpiry,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
if ($ConfirmOS -and $Example -ne 'ex01') { throw '-ConfirmOS belongs to ex01.' }
if ($VerifyExpiry -and $Example -ne 'ex02') { throw '-VerifyExpiry belongs to ex02.' }
$unit06Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit06Root 'manifest.json') -Raw | ConvertFrom-Json
$match = @((@($manifest.examples) + @($manifest.exercises)) |
    Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Use v01 through v07, ex01 or ex02.' }
$project = Join-Path $unit06Root $match[0].project
$forwardArgs = @('run', '--project', $project)
if ($NoBuild) { $forwardArgs += '--no-build' }
$forwardArgs += '--'
if ($Demo) { $forwardArgs += '--demo' }
if ($Live) { $forwardArgs += '--live' }
if ($Session) { $forwardArgs += @('--session', $Session) }
if ($PSBoundParameters.ContainsKey('InputText')) { $forwardArgs += @('--input', $InputText) }
if ($Replica) { $forwardArgs += @('--replica', $Replica) }
if ($Audit) { $forwardArgs += '--audit' }
if ($ConfirmOS) { $forwardArgs += @('--confirm-os', $ConfirmOS) }
if ($VerifyExpiry) { $forwardArgs += '--verify-expiry' }
& dotnet @forwardArgs
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed. Inspect its JSON error code." }

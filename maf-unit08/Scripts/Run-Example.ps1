param(
    [string]$Example = 'v07',
    [ValidateSet('vpn','payroll','combined')][string]$Scenario,
    [switch]$Demo,
    [switch]$Live,
    [switch]$FailNetwork,
    [switch]$Conflict,
    [switch]$Serial,
    [switch]$DenyIdentity,
    [Nullable[int]]$Cases,
    [Nullable[int]]$NetworkDelayMs,
    [Nullable[int]]$ServiceDelayMs,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
$unit08Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit08Root 'manifest.json') -Raw | ConvertFrom-Json
$match = @((@($manifest.examples) + @($manifest.exercises)) |
    Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Use v01 through v10, ex01 or ex02.' }
if ($DenyIdentity -and $match[0].id -ne 'ex01') { throw '-DenyIdentity belongs to ex01.' }
if ($null -ne $Cases -and $match[0].id -ne 'v08') { throw '-Cases belongs to v08.' }
$project = Join-Path $unit08Root $match[0].project
$forwardArgs = @('run', '--project', $project)
if ($NoBuild) { $forwardArgs += '--no-build' }
$forwardArgs += '--'
if ($Scenario) { $forwardArgs += $Scenario }
if ($Demo) { $forwardArgs += '--demo' }
if ($Live) { $forwardArgs += '--live' }
if ($FailNetwork) { $forwardArgs += '--fail-network' }
if ($Conflict) { $forwardArgs += '--conflict' }
if ($Serial) { $forwardArgs += '--serial' }
if ($DenyIdentity) { $forwardArgs += '--deny-identity' }
if ($null -ne $Cases) { $forwardArgs += @('--cases', [string]$Cases) }
if ($null -ne $NetworkDelayMs) { $forwardArgs += @('--network-delay-ms', [string]$NetworkDelayMs) }
if ($null -ne $ServiceDelayMs) { $forwardArgs += @('--service-delay-ms', [string]$ServiceDelayMs) }
& dotnet @forwardArgs
$runExitCode = $LASTEXITCODE
if ($runExitCode -notin @(0, 2)) { throw "Example $Example failed. Inspect its failure event." }
if ($runExitCode -eq 2) { Write-Host 'Review required: the application returned business exit code 2.' }

param(
    [string]$Example = 'v01',
    [switch]$Demo,
    [switch]$Live,
    [switch]$Interactive,
    [string]$Case,
    [string]$InputText,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
$unit03PackageRoot = Split-Path $PSScriptRoot -Parent
$unit03Manifest = Get-Content -LiteralPath (Join-Path $unit03PackageRoot 'manifest.json') -Raw | ConvertFrom-Json
$unit03Targets = @($unit03Manifest.examples) + @($unit03Manifest.exercises)
$unit03Match = @($unit03Targets | Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($unit03Match.Count -ne 1) { throw 'Use v01 through v08, ex01 or ex02.' }
$unit03ProjectPath = Join-Path $unit03PackageRoot $unit03Match[0].project
$unit03ForwardArgs = @('run', '--project', $unit03ProjectPath)
if ($NoBuild) { $unit03ForwardArgs += '--no-build' }
$unit03ForwardArgs += '--'
if ($Demo) { $unit03ForwardArgs += '--demo' }
if ($Live) { $unit03ForwardArgs += '--live' }
if ($Interactive) { $unit03ForwardArgs += '--interactive' }
if ($Case) { $unit03ForwardArgs += @('--case', $Case) }
if ($InputText) { $unit03ForwardArgs += @('--input', $InputText) }
& dotnet @unit03ForwardArgs
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed. Review its preceding output." }

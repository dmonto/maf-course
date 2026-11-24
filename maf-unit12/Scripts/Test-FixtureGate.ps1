[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unitRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $unitRoot 'v07-testing-automation/Unit12.V07.csproj'
$buildArgs = @('run', '--project', $project, '--configuration', $Configuration)
if ($NoBuild) { $buildArgs += '--no-build' }
& dotnet @buildArgs -- fixture
if ($LASTEXITCODE -ne 0) { throw 'The positive fixture must return 0.' }
& dotnet @buildArgs -- fixture-bad
if ($LASTEXITCODE -ne 1) { throw 'The known negative must return 1.' }
Write-Host 'Fixture gate passed: actual child-process exit codes were 0 and 1.'

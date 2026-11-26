[CmdletBinding()]
param(
    [ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02')]
    [string]$Example = 'v01',
    [switch]$Live,
    [switch]$Http,
    [switch]$RequireAzure,
    [switch]$InjectRegression,
    [string]$BaseUrl,
    [string]$Report,
    [switch]$NoBuild,
    [ValidateSet(0,1)][int]$ExpectedExitCode = 0
)
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
$projects = @{
    v01='v01-architecture/Unit16.V01.csproj'; v02='v02-prompts/Unit16.V02.csproj'
    v03='v03-tools/Unit16.V03.csproj'; v04='v04-memory/Unit16.V04.csproj'
    v05='v05-workflow/Unit16.V05.csproj'; v06='v06-evaluation/Unit16.V06.csproj'
    v07='v07-validation/Unit16.V07.csproj'; v08='v08-debt/Unit16.V08.csproj'
    v09='v09-rollout/Unit16.V09.csproj'; v10='v10-best-practices/Unit16.V10.csproj'
    ex01='exercise-solutions/01-stop-rule/Unit16.EX01.csproj'
    ex02='exercise-solutions/02-canary/Unit16.EX02.csproj'
}
$project = Join-Path $unitRoot $projects[$Example]
$applicationArgs = @()
if ($Live) { $applicationArgs += '--live' }
if ($Http) { $applicationArgs += '--http' }
if ($RequireAzure) { $applicationArgs += '--require-azure' }
if ($InjectRegression) { $applicationArgs += '--inject-regression' }
if ($BaseUrl) { $applicationArgs += @('--base', $BaseUrl) }
if ($Report) { $applicationArgs += @('--report', [IO.Path]::GetFullPath($Report)) }
if (-not $NoBuild) {
    Write-Host ('[COMMAND] dotnet build "{0}" -p:RestoreLockedMode=true -m:1' -f $project)
    & dotnet build $project '-p:RestoreLockedMode=true' '-m:1'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$runArgs = @('run', '--project', $project, '--no-build', '--') + $applicationArgs
Write-Host ('[COMMAND] dotnet ' + ($runArgs -join ' '))
& dotnet @runArgs
$actualExitCode = $LASTEXITCODE
if ($actualExitCode -ne $ExpectedExitCode) {
    throw "Example $Example exited with $actualExitCode; expected $ExpectedExitCode."
}
if ($ExpectedExitCode -eq 1) {
    Write-Host '[CONCLUSION] The application returned the expected failure. The candidate was rejected.'
} else {
    Write-Host ('[CONCLUSION] {0} completed successfully.' -f $Example)
}

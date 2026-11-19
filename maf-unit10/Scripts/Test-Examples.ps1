param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit10TestRoot = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    & dotnet build (Join-Path $unit10TestRoot 'Unit10.slnx') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Unit 10 build failed.' }
}
$manifest = Get-Content (Join-Path $unit10TestRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($item in @($manifest.examples) + @($manifest.exercises) + @($manifest.verification)) {
    $testArguments = @('run', '--project', (Join-Path $unit10TestRoot $item.project), '--no-build', '--', '--demo')
    if ($item.id -eq 'v09') { $testArguments += @('--case', 'acceptance') }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw "Local test failed: $($item.id)." }
}
# Verify that a permission failure returns a nonzero native process exit status.
$deniedProject = Join-Path $unit10TestRoot 'v03-slack-integration/Unit10.V03.csproj'
& dotnet run --project $deniedProject --no-build -- --demo --profile employee
if ($LASTEXITCODE -eq 0) { throw 'The expected read-only notification denial did not fail.' }
Write-Host 'All Unit 10 local checks passed. No Azure inference or real Slack send was requested.'

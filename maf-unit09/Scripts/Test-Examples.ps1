$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit09TestRoot = Split-Path $PSScriptRoot -Parent
$unit09TestManifest = Get-Content (Join-Path $unit09TestRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($unit09TestItem in (@($unit09TestManifest.examples) + @($unit09TestManifest.verification))) {
    & dotnet build (Join-Path $unit09TestRoot $unit09TestItem.project) --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($unit09TestItem.id)" }
    & $PSScriptRoot/Run-Example.ps1 -Example $unit09TestItem.id -Demo -NoBuild
}
Write-Host 'All example runs, boundary checks and expected-failure checks passed.'

$ErrorActionPreference='Stop'
$unit05Root=Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $unit05Root 'Verification/Unit05.Checks.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Offline verification failed.' }

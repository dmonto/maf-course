$ErrorActionPreference = 'Stop'
$unit04Root = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $unit04Root 'Verification/Unit04.Checks.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Offline verification failed.' }

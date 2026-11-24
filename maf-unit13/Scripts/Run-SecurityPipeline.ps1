[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit13Root = Split-Path -Parent $PSScriptRoot
& dotnet restore (Join-Path $unit13Root 'Maf.Unit13.slnx') --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
& dotnet build (Join-Path $unit13Root 'Maf.Unit13.slnx') --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
# v10 propagates the actual suite and gate results. This stage never selects an older report.
& (Join-Path $PSScriptRoot 'Run-Example.ps1') -Example v10 -Mode fixture -NoBuild
if ($LASTEXITCODE -ne 0) { throw 'Security pipeline stage failed.' }

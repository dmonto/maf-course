#requires -Version 5.1
param(
    [Parameter(Mandatory)][ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02','checks')][string]$Example,
    [ValidateSet('fixture','azure')][string]$Mode='fixture',
    [switch]$NoBuild,[switch]$Serve
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
if ($Mode -eq 'azure' -and $Example -notin @('v01','v05')) { throw 'Use v01 or v05 for Azure-mode execution.' }
if ($Serve -and $Example -notin @('v01','v05')) { throw 'Serve is supported by v01 and v05.' }
$unit14Entry=(Get-Content (Join-Path $unit14Root 'examples.json') -Raw -Encoding UTF8 | ConvertFrom-Json) |
    ForEach-Object { $_ } | Where-Object id -eq $Example
if (-not $unit14Entry) { throw "Example $Example not found in examples.json." }
$unit14Args=@('run','--project',(Join-Path $unit14Root $unit14Entry.project),'--no-launch-profile')
if ($NoBuild) { $unit14Args+='--no-build' }
$unit14Args+=@('--','--mode',$Mode)
if ($Serve) { $unit14Args+='--serve' }
& dotnet @unit14Args
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed." }

[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10','ex01','ex02','checks')][string]$Example,
    [ValidateSet('fixture','azure')][string]$Mode = 'fixture',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [string]$Report
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unit13Root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
if ($Mode -eq 'azure' -and $Example -notin @('v04','v10')) { throw 'Azure mode is supported by v04 and v10.' }
if ($Report -and $Example -ne 'v09') { throw '-Report is supported by v09.' }
if ($Example -eq 'checks') {
    $unit13Project = Join-Path $unit13Root 'Verification/Unit13.Checks.csproj'
} else {
    $unit13Manifest = Get-Content -LiteralPath (Join-Path $unit13Root 'examples.json') -Raw | ConvertFrom-Json
    $unit13Entries = @($unit13Manifest | Where-Object id -EQ $Example)
    if ($unit13Entries.Count -ne 1) { throw 'Example project was not found.' }
    $unit13Project = Join-Path $unit13Root $unit13Entries[0].project
}
$unit13Args = @('run','--project',$unit13Project,'--configuration',$Configuration)
if ($NoBuild) { $unit13Args += '--no-build' }
$unit13Args += @('--',$Mode)
if ($Report) { $unit13Args += @('--report',(Resolve-Path -LiteralPath $Report).Path) }
& dotnet @unit13Args
$unit13Exit = $LASTEXITCODE
exit $unit13Exit

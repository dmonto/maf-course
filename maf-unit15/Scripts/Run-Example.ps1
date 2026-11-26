param(
    [Parameter(Mandatory=$true)][ValidatePattern('^v(0[1-9]|10)$')][string]$Example,
    [string]$Mode = '', [string]$Service = '', [string]$State = '',
    [string]$OperationId = '', [string]$BaseUrl = '', [switch]$Live, [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Helpers.ps1')
if ($Live) {
    if ($Mode -and $Mode -ne 'azure') { throw '-Live selects azure mode.' }
    $Mode = 'azure'
}
if ($Mode -and $Mode -notin @('fixture','http','azure')) { throw 'Mode must be fixture, http or azure.' }
$manifest = Get-Content -LiteralPath (Join-Path (Get-Unit15Root) 'examples.json') -Raw | ConvertFrom-Json
$entry = @($manifest.examples | Where-Object { $_.id -eq $Example })
if ($entry.Count -ne 1) { throw "Unknown example $Example." }
$applicationArguments = @()
if ($Mode) { $applicationArguments += @('--mode', $Mode) }
if ($Service) { $applicationArguments += @('--service', $Service) }
if ($State) { $applicationArguments += @('--state', $State) }
if ($OperationId) { $applicationArguments += @('--operation-id', $OperationId) }
if ($BaseUrl) { $applicationArguments += @('--base', $BaseUrl) }
Write-Host "[START] Example $Example | mode=$(if ($Mode) { $Mode } else { 'default (fixture)' }) | service=$(if ($Service) { $Service } else { 'default (vpn)' })"
Invoke-Unit15Project -Project $entry[0].project -ApplicationArguments $applicationArguments -NoBuild:$NoBuild

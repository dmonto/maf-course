param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$unit07Root = Split-Path $PSScriptRoot -Parent
if (-not $NoBuild) {
    dotnet build (Join-Path $unit07Root 'Maf.Unit07.slnx') -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$manifest = Get-Content (Join-Path $unit07Root 'manifest.json') -Raw | ConvertFrom-Json
$prefix = 'smoke-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
function Invoke-Check {
    param([string]$Id, [string[]]$Cli, [int]$ExpectedExit = 0, [string]$ExpectedText)
    $entry = @((@($manifest.examples) + @($manifest.exercises)) | Where-Object { $_.id -eq $Id })[0]
    $project = Join-Path $unit07Root $entry.project
    $output = @(& dotnet run --no-build --project $project -- @Cli --demo 2>&1)
    $exitCode = $LASTEXITCODE
    $text = $output -join "`n"
    if ($exitCode -ne $ExpectedExit) { throw "${Id}: unexpected exit code $exitCode. $text" }
    if ($ExpectedText -and -not $text.Contains($ExpectedText)) { throw "${Id}: missing $ExpectedText. $text" }
    if ($ExpectedExit -ne 0 -and $text.Contains('RESULT ')) { throw "${Id}: failed run yielded a result." }
    Write-Host "PASS $Id $($Cli -join ' ')"
}
foreach ($id in 'v01','v02','v05','v07','v09') {
    Invoke-Check $id @('run','vpn','--state',"$prefix-$id") 0 'information-ready'
}
Invoke-Check 'v02' @('run','payroll','--decision','yes','--state',"$prefix-route") 0 'escalation-approved'
Invoke-Check 'v03' @('run','payroll','--fail','--state',"$prefix-fail") 1 'status-timeout'
Invoke-Check 'v04' @('run','payroll','--decision','no','--state',"$prefix-no") 0 'escalation-rejected'
Invoke-Check 'v05' @('run','unknown','--state',"$prefix-invalid") 1 'invalid-request'
Invoke-Check 'v06' @('run','payroll','--decision','yes','--state',"$prefix-order") 0 'order-verified'
Invoke-Check 'v07' @('run','vpn','--serial','--state',"$prefix-serial") 0 'information-ready'
Invoke-Check 'v08' @('pause','payroll','--state',"$prefix-restart") 0 'paused'
Invoke-Check 'v08' @('resume','--decision','yes','--state',"$prefix-restart") 0 'escalation-approved'
Invoke-Check 'ex01' @('run','vpn','--state',"$prefix-fresh") 0 'information-ready'
Invoke-Check 'ex01' @('run','payroll','--stale','--state',"$prefix-stale") 1 'stale-or-future-evidence'
Invoke-Check 'ex02' @('run','payroll','--decision','no','--state',"$prefix-reject") 0 'escalation-rejected'
Invoke-Check 'ex02' @('run','payroll','--decision','yes','--fail-after-save','--state',"$prefix-recovery") 1 'injected-failure-after-save'
Invoke-Check 'ex02' @('resume','--state',"$prefix-recovery") 0 '"reused":true'
Write-Host 'All deterministic smoke checks passed. Live inference was not called.'

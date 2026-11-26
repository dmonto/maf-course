#requires -Version 5.1
param([switch]$InternalChild)
# Exercise command boundaries without requiring an installed container engine.
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scripts/Compatibility.ps1')
if (-not $InternalChild) {
    $unit14Shell=Get-UnitShellPath
    & $unit14Shell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -InternalChild
    if ($LASTEXITCODE -ne 0) { throw 'Simulated Podman lifecycle checks failed.' }
    return
}
$unit14TestRoot=Split-Path -Parent $PSScriptRoot
$global:Unit14PodmanCalls=[System.Collections.Generic.List[object]]::new()
$global:Unit14MachineState='ready'
$global:Unit14ProbeFails=$false
$env:Runner__Release='14.1.0'
function Assert-Test([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function global:dotnet {
    Assert-Test ($args -contains (Join-Path $unit14TestRoot 'Shared/Unit14.Probe/Unit14.Probe.csproj')) 'Unexpected probe project.'
    $global:LASTEXITCODE=if ($global:Unit14ProbeFails) { 17 } else { 0 }
}
function global:podman {
    $global:LASTEXITCODE=0
    $unit14Args=@($args)
    $global:Unit14PodmanCalls.Add($unit14Args)
    switch ($unit14Args[0]) {
        'info' {
            if ($global:Unit14MachineState -notin @('ready','running')) { $global:LASTEXITCODE=1; return }
            '{}'; return
        }
        'machine' {
            if ($unit14Args[1] -eq 'list') {
                switch ($global:Unit14MachineState) {
                    'missing' { '[]' }
                    'stopped' { '[{"Name":"unit14-test","Running":false}]' }
                    default { '[{"Name":"unit14-test","Running":true}]' }
                }
            } elseif ($unit14Args[1] -eq 'start') { $global:Unit14MachineState='ready' }
            return
        }
        'run' { 'synthetic-container-id'; return }
        'port' { '127.0.0.1:49152'; return }
        'logs' { 'synthetic-container-log'; return }
        'rm' { return }
        default { throw "Unexpected Podman command: $($unit14Args[0])" }
    }
}
foreach ($unit14State in @('ready','running','stopped','missing')) {
    $global:Unit14PodmanCalls.Clear()
    $global:Unit14MachineState=$unit14State
    $unit14Parameters=if ($unit14State -eq 'ready') { @{} } else { @{MachineName='unit14-test'} }
    & (Join-Path $unit14TestRoot 'Scripts/Initialize-Podman.ps1') @unit14Parameters
    $unit14Init=@($global:Unit14PodmanCalls | Where-Object { $_[0] -eq 'machine' -and $_[1] -eq 'init' })
    $unit14Start=@($global:Unit14PodmanCalls | Where-Object { $_[0] -eq 'machine' -and $_[1] -eq 'start' })
    Assert-Test ($unit14Init.Count -eq $(if ($unit14State -eq 'missing') { 1 } else { 0 })) 'Unexpected machine initialization.'
    Assert-Test ($unit14Start.Count -eq $(if ($unit14State -in @('missing','stopped')) { 1 } else { 0 })) 'Unexpected machine start.'
}
foreach ($unit14Scenario in @('success','probe-failure','keep')) {
    $global:Unit14PodmanCalls.Clear()
    $global:Unit14MachineState='ready'
    $global:Unit14ProbeFails=$unit14Scenario -eq 'probe-failure'
    $unit14Failed=$false
    try { & (Join-Path $unit14TestRoot 'Scripts/Test-Container.ps1') -KeepContainer:($unit14Scenario -eq 'keep') }
    catch { $unit14Failed=$true }
    Assert-Test ($unit14Failed -eq $global:Unit14ProbeFails) 'Probe failure was lost or success failed.'
    $unit14Run=@($global:Unit14PodmanCalls | Where-Object { $_[0] -eq 'run' })[0]
    Assert-Test ($unit14Run -contains '127.0.0.1::8080') 'Container must use an available loopback port.'
    Assert-Test ($unit14Run[-1] -eq 'localhost/maf-status:14.1.0') 'Unexpected local image reference.'
    $unit14Remove=@($global:Unit14PodmanCalls | Where-Object { $_[0] -eq 'rm' })
    Assert-Test ($unit14Remove.Count -eq $(if ($unit14Scenario -eq 'keep') { 0 } else { 1 })) 'Incorrect cleanup behavior.'
    if ($unit14Remove.Count) { Assert-Test ($unit14Remove[0][2] -eq $unit14Run[3]) 'Cleanup targeted another container.' }
}
Write-Host 'ALL SIMULATED PODMAN LIFECYCLE CHECKS PASSED'

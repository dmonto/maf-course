#requires -Version 5.1
param([string]$MachineName)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not (Get-Command podman -ErrorAction SilentlyContinue)) { throw 'Install Podman and reopen PowerShell.' }
Write-UnitTrace -Kind 'MILESTONE' -Text 'Is the Podman engine already answering?'
$unit14Ready=Test-UnitTool -Executable podman -Arguments @('info','--format','json')
if ($unit14Ready -and -not $MachineName) { Write-UnitTrace -Kind 'CONCLUSION' -Text 'Podman is ready.'; return }
if (-not $unit14OnWindows -and -not $unit14OnMacOS -and -not $MachineName) {
    throw 'Start or repair the local Podman service/connection. Linux does not require a Podman VM.'
}
Write-UnitTrace -Kind 'MILESTONE' -Text 'Podman is not answering; inspect the Podman machines (VM) and start one.'
$unit14MachinesText=@(Invoke-UnitTool -Executable podman -Arguments @('machine','list','--format','json')) -join "`n"
$unit14Machines=@($unit14MachinesText | ConvertFrom-Json)
if (-not $MachineName) {
    if ($unit14Machines.Count -gt 0) {
        $unit14Selected=@($unit14Machines | Where-Object Running | Select-Object -First 1)
        if (-not $unit14Selected) { $unit14Selected=@($unit14Machines | Where-Object Default | Select-Object -First 1) }
        if (-not $unit14Selected) { $unit14Selected=@($unit14Machines | Select-Object -First 1) }
        $MachineName=$unit14Selected[0].Name
    } else { $MachineName='podman-machine-default' }
}
$unit14Existing=@($unit14Machines | Where-Object Name -eq $MachineName)
if ($unit14Existing.Count -eq 0) {
    $null=Invoke-UnitTool -Executable podman -Arguments @('machine','init',$MachineName)
}
if ($unit14Existing.Count -eq 0 -or -not $unit14Existing[0].Running) {
    $null=Invoke-UnitTool -Executable podman -Arguments @('machine','start',$MachineName)
}
Assert-PodmanReady
Write-UnitTrace -Kind 'CONCLUSION' -Text 'Podman is ready.'

param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
if (-not $NoBuild) { & "$PSScriptRoot\Build.ps1" }
foreach ($n in 1..10) {
    $v = 'v{0:d2}' -f $n
    & "$PSScriptRoot\Run-Example.ps1" -Example $v -NoBuild
}
& "$PSScriptRoot\Run-Example.ps1" -Example v01 -NoBuild -ExampleArgs @('--service','email')
& "$PSScriptRoot\Run-Example.ps1" -Example v02 -NoBuild -ExampleArgs @('--invalid')
& "$PSScriptRoot\Run-Example.ps1" -Example v09 -NoBuild -ExampleArgs @('--exception-rate','0.4')
& "$PSScriptRoot\Run-Example.ps1" -Example v10 -NoBuild -ExampleArgs @('--stop-writes')
Write-Host '[CONCLUSION] All ten offline examples returned success.'

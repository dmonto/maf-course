#requires -Version 5.1
param([Parameter(Mandatory)][string]$BaseUri,[string]$Release,
    [ValidateSet('fixture','azure')][string]$Mode='fixture',[string]$Output,
    [ValidateRange(1,20)][int]$Requests=4,[ValidateRange(1,4)][int]$Parallel=1,
    [ValidateRange(1,300)][int]$TimeoutSeconds=120)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $Release) { $Release=$env:Runner__Release }
if (-not $Output) { $Output=Join-Path (New-UnitArtifactDirectory -Name 'probe') 'smoke.json' }
$unit14Python=if ($env:PYTHON_COMMAND) { $env:PYTHON_COMMAND } else { 'python' }
Write-Host "Probe evidence: $Output"
$unit14ProbeStarted=[DateTime]::UtcNow
try {
    $null=Invoke-UnitTool -Executable $unit14Python -Arguments @((Join-Path $unit14Root 'Python/smoke.py'),
        '--base',$BaseUri,'--release',$Release,'--mode',$Mode,'--requests',"$Requests",'--parallel',"$Parallel",
        '--timeout',"$TimeoutSeconds",'--output',$Output)
} catch {
    # A previous report must not be mistaken for evidence from this failed invocation.
    if ((Test-Path -LiteralPath $Output) -and (Get-Item -LiteralPath $Output).LastWriteTimeUtc -ge $unit14ProbeStarted) {
        try {
            $unit14Report=Get-Content -LiteralPath $Output -Raw -Encoding UTF8 | ConvertFrom-UnitJson
            Write-Host "Probe failed at $($unit14Report.failureStage): $($unit14Report.failurePath)"
            foreach ($unit14Row in $unit14Report.results) {
                Write-Host "Service=$($unit14Row.service) HTTP=$($unit14Row.httpStatus) Phase=$($unit14Row.phase) Failure=$($unit14Row.failureType)"
            }
        } catch { Write-Host 'The probe report could not be read.' }
    }
    throw
}

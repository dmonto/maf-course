#requires -Version 5.1
param([Parameter(Mandatory)][string]$BaseUri,[string]$Release,
    [ValidateSet('fixture','azure')][string]$Mode='fixture',[string]$Output,
    [ValidateRange(1,20)][int]$Requests=4,[ValidateRange(1,4)][int]$Parallel=1,
    [ValidateRange(1,300)][int]$TimeoutSeconds=120)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $Release) { $Release=$env:Runner__Release }
if (-not $Output) { $Output=Join-Path (New-UnitArtifactDirectory -Name 'probe') 'smoke.json' }
Write-UnitTrace -Kind 'MILESTONE' -Text "Run the C# probe against $BaseUri (release $Release, mode $Mode); report: $Output"
$unit14ProbeStarted=[DateTime]::UtcNow
try {
    $null=Invoke-UnitTool -Executable dotnet -Arguments @('run','--project',
        (Join-Path $unit14Root 'Shared/Unit14.Probe/Unit14.Probe.csproj'),'--no-launch-profile','--',
        '--base',$BaseUri,'--release',$Release,'--mode',$Mode,'--requests',"$Requests",'--parallel',"$Parallel",
        '--timeout',"$TimeoutSeconds",'--output',$Output) -Echo
} catch {
    # A previous report must not be mistaken for evidence from this failed invocation.
    if ((Test-Path -LiteralPath $Output) -and (Get-Item -LiteralPath $Output).LastWriteTimeUtc -ge $unit14ProbeStarted) {
        try {
            $unit14Report=Get-Content -LiteralPath $Output -Raw -Encoding UTF8 | ConvertFrom-UnitJson
            Write-UnitTrace -Kind 'FAILURE' -Text "Probe failed at $($unit14Report.failureStage): $($unit14Report.failurePath)"
            foreach ($unit14Row in $unit14Report.results) {
                Write-UnitTrace -Kind 'FAILURE' -Text "Service=$($unit14Row.service) HTTP=$($unit14Row.httpStatus) Phase=$($unit14Row.phase) Failure=$($unit14Row.failureType)"
            }
        } catch { Write-UnitTrace -Kind 'FAILURE' -Text 'The probe report could not be read.' }
    }
    throw
}
Write-UnitTrace -Kind 'CONCLUSION' -Text "Probe passed. Evidence: $Output"

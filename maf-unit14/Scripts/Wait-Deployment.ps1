#requires -Version 5.1
param([Parameter(Mandatory)][string]$BaseUri,[string]$Release,
    [ValidateSet('fixture','azure')][string]$Mode='fixture',
    [ValidateRange(1,600)][int]$TimeoutSeconds=180)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $Release) { $Release=$env:Runner__Release }
$unit14Base=[uri]$BaseUri
if (-not $unit14Base.IsAbsoluteUri -or $unit14Base.Scheme -notin @('http','https') -or
    $unit14Base.UserInfo -or $unit14Base.AbsolutePath -ne '/' -or $unit14Base.Query -or $unit14Base.Fragment) {
    throw 'Use an HTTP(S) origin without credentials, a path, a query or a fragment.'
}
if ($unit14Base.Scheme -eq 'http' -and $unit14Base.Host -notin @('localhost','127.0.0.1','[::1]','::1')) {
    throw 'Remote targets require HTTPS.'
}
Write-UnitTrace -Kind 'MILESTONE' -Text "Poll GET $($BaseUri.TrimEnd('/'))/health/ready until release $Release / mode $Mode answers (max $TimeoutSeconds s)."
$unit14Until=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$unit14Headers=@{'Cache-Control'='no-cache'}
if ($env:SMOKE_BEARER_TOKEN) { $unit14Headers.Authorization="Bearer $env:SMOKE_BEARER_TOKEN" }
$unit14Observed='no matching health response'
do {
    $unit14Remaining=[Math]::Ceiling(($unit14Until-[DateTime]::UtcNow).TotalSeconds)
    if ($unit14Remaining -le 0) { break }
    try {
        Write-UnitTrace -Kind 'HTTP REQUEST' -Text "GET $($BaseUri.TrimEnd('/'))/health/ready"
        $unit14Health=Invoke-RestMethod -Uri ($BaseUri.TrimEnd('/')+'/health/ready') -Headers $unit14Headers `
            -TimeoutSec ([Math]::Min(10,$unit14Remaining)) -MaximumRedirection 0 -ErrorAction Stop
        if ($unit14Health.status -eq 'ok' -and $unit14Health.release -eq $Release -and $unit14Health.mode -eq $Mode -and
            $unit14Health.promptVersion -eq 'status-v1' -and $unit14Health.deadlinePolicy -eq 'bounded-wait-v1') {
            Write-UnitTrace -Kind 'CONCLUSION' -Text "Ready: release $Release; mode $Mode; bounded-wait-v1"
            return $unit14Health
        }
        # Do not echo untrusted response fields or authentication/error bodies.
        $unit14Observed='health returned, but release/mode/policy did not match'
    } catch { $unit14Observed='health is temporarily unavailable' }
    Write-UnitTrace -Kind 'WAIT' -Text "Not ready yet: $unit14Observed; retrying."
    $unit14Remaining=($unit14Until-[DateTime]::UtcNow).TotalSeconds
    if ($unit14Remaining -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Min(2000,$unit14Remaining*1000)) }
} while ([DateTime]::UtcNow -lt $unit14Until)
throw "Expected release $Release in mode $Mode was not ready within $TimeoutSeconds seconds: $unit14Observed."

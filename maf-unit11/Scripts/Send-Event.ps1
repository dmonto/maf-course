[CmdletBinding()]
param(
    [int]$Port = 5111,
    [string]$Id = '',
    [ValidateSet('vpn','erp')][string]$Service = 'vpn',
    [ValidateSet('healthy','degraded','unavailable')][string]$Status = 'degraded',
    [string]$Tenant = 'training-a',
    [string]$EventType = 'ServiceStatusChanged.v1',
    [int]$AgeMinutes = 0,
    [string]$SaveAs = '',
    [string]$File = '',
    [switch]$CreateOnly,
    [switch]$Wait
)
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
if ($File) {
    $savedFile = (Resolve-Path -LiteralPath $File).Path
    $body = [System.IO.File]::ReadAllText($savedFile)
} else {
    if (-not $Id) { $Id = ('EVT-' + [guid]::NewGuid().ToString('N')).ToUpperInvariant() }
    $event = @{
        id = $Id
        type = $EventType
        tenant = $Tenant
        correlationId = [guid]::NewGuid().ToString()
        occurredAtUtc = [DateTimeOffset]::UtcNow.AddMinutes(-$AgeMinutes).ToString('O')
        data = @{
            service = $Service
            status = $Status
            summary = "Synthetic training observation for $Service."
        }
    }
    $body = $event | ConvertTo-Json -Depth 6
    if (-not $SaveAs) { $SaveAs = Join-Path $unitRoot '.data/sample-event.json' }
    $savedFile = [System.IO.Path]::GetFullPath($SaveAs)
    $parentDirectory = Split-Path $savedFile -Parent
    New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
    [System.IO.File]::WriteAllText($savedFile, $body, [System.Text.UTF8Encoding]::new($false))
}
$event = $body | ConvertFrom-Json
if ($CreateOnly) {
    [pscustomobject]@{ eventId = $event.id; file = $savedFile; submitted = $false }
    return
}
$root = "http://127.0.0.1:$Port"
$accepted = Invoke-RestMethod "$root/events" -Method Post -ContentType 'application/json' -Body $body
$accepted
Write-Host "Saved envelope: $savedFile"
if ($Wait) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(160)
    do {
        $job = Invoke-RestMethod "$root/jobs/$($event.id)"
        if ($job.state -in 'Completed', 'Dead') {
            $job
            if ($job.state -eq 'Dead') { throw "Event $($event.id) is quarantined: $($job.errorCode)." }
            return
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw 'The event did not reach a terminal state before the observation deadline.'
}

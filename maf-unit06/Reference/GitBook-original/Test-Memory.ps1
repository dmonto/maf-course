$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
Push-Location $projectRoot
try {
    $dll = Join-Path $projectRoot 'bin\Debug\net10.0\Unit06.Memory.dll'
    if (-not (Test-Path $dll)) { throw 'Build Unit06.Memory first.' }
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $session = "memory-$suffix"
    function Invoke-Turn([string]$Id, [string]$Text) {
        $lines = & dotnet $dll $Id $Text
        if ($LASTEXITCODE -ne 0) { throw "Turn failed for $Id." }
        return (($lines -join "`n") | ConvertFrom-Json)
    }
    $first = Invoke-Turn $session 'My test marker is copper-51. I use Windows 11 and VPN error 809.'
    if ($first.restored -or $first.revision -ne 1) { throw 'Expected a new session.' }
    $second = Invoke-Turn $session 'What test marker, operating system and VPN error did I report?'
    if (-not $second.restored -or $second.revision -ne 2 -or
        $second.answer -notmatch 'copper-51' -or $second.answer -notmatch 'Windows' -or
        $second.answer -notmatch '809') { throw 'Restart recall check failed.' }
    $fresh = Invoke-Turn "fresh-$suffix" 'What test marker did I report?'
    if ($fresh.restored -or $fresh.answer -match 'copper-51') {
        throw 'Fresh-session isolation check failed.'
    }
    $drops = 0
    for ($i = 1; $i -le 5; $i++) {
        $turn = Invoke-Turn $session "Record check-in $i. Reply briefly."
        $drops += $turn.droppedTurns
        if ($turn.historyChars -gt 16000) { throw 'History budget exceeded.' }
    }
    if ($drops -lt 1) { throw 'Expected old turns to be removed.' }
    $auditLines = & dotnet $dll $session '--audit'
    if ($LASTEXITCODE -ne 0) { throw 'Audit read failed.' }
    $audit = @((($auditLines -join "`n") | ConvertFrom-Json))
    if ($audit.Count -ne 7 -or $audit[0].Revision -ne 7) {
        throw 'Expected seven committed turns.'
    }
    Write-Host 'PASS: restart recall, fresh-session separation, bounded history and commit audit.'
    Write-Host "Session: $session"
    Write-Host "Last answer: $($turn.answer)"
}
finally { Pop-Location }

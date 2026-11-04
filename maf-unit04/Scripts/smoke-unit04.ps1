$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5050'

function Assert-HttpFailure {
    param([hashtable]$Body, [int]$Expected)
    try {
        $null = Invoke-RestMethod "$baseUrl/api/chat" -Method Post `
            -ContentType 'application/json' `
            -Body ($Body | ConvertTo-Json -Depth 10)
        throw "Expected HTTP $Expected, but the request succeeded."
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        $actual = [int]$_.Exception.Response.StatusCode
        if ($actual -ne $Expected) { throw "Expected $Expected; received $actual." }
    }
}

$health = Invoke-RestMethod "$baseUrl/health"
if ($health.status -ne 'ok' -or $health.agent -ne 'service-desk') {
    throw 'Host readiness check failed.'
}
Assert-HttpFailure -Body @{ message = '' } -Expected 400
Assert-HttpFailure -Body @{
    message = 'What is the current status of payroll?'
    conversationId = '00000000000000000000000000000000'
} -Expected 404

$result = Invoke-RestMethod "$baseUrl/api/chat" -Method Post `
    -ContentType 'application/json' `
    -Body (@{ message = 'What is the current status of payroll?' } |
        ConvertTo-Json -Depth 10)
if ($result.outcome -ne 'completed' -or
    [string]::IsNullOrWhiteSpace($result.answer)) {
    throw 'No completed answer was returned.'
}
$matched = @($result.evidence | Where-Object {
    $_.ok -eq $true -and $_.data.service -eq 'payroll' -and
    $_.data.status -eq 'degraded'
})
if ($matched.Count -eq 0) { throw 'The payroll lookup evidence is missing.' }
$result | ConvertTo-Json -Depth 10
Write-Host 'PASS: host, input checks and live payroll tool evidence.'

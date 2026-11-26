[CmdletBinding()]
param([string]$BaseUrl = 'http://127.0.0.1:8082', [switch]$RequireAzure)
$ErrorActionPreference = 'Stop'
$rootUrl = $BaseUrl.TrimEnd('/')
Write-Host ('[HTTP REQUEST] GET {0}/health/ready' -f $rootUrl)
$health = Invoke-RestMethod -Method Get -Uri "$rootUrl/health/ready"
$health | ConvertTo-Json -Depth 10 | Write-Host
Write-Host '[MILESTONE] Readiness passed; now check the functional POST endpoint.'
foreach ($service in @('vpn','email')) {
    $body = @{ service = $service } | ConvertTo-Json -Compress
    Write-Host ('[HTTP REQUEST] POST {0}/api/status' -f $rootUrl)
    Write-Host ('[HTTP JSON BODY] ' + $body)
    $answer = Invoke-RestMethod -Method Post -Uri "$rootUrl/api/status" -ContentType 'application/json' -Body $body
    $answer | ConvertTo-Json -Depth 10 | Write-Host
    $expectedStatus = if ($service -eq 'vpn') { 'degraded' } else { 'healthy' }
    if ($answer.service -ne $service -or $answer.status -ne $expectedStatus) { throw 'Evidence contract mismatch.' }
    if ($RequireAzure -and $answer.mode -ne 'azure') { throw 'Expected Azure mode; a fixture result cannot pass this gate.' }
    Write-Host ('[CHECK] PASS | {0}/{1}; mode={2}' -f $answer.service, $answer.status, $answer.mode)
}
Write-Host '[CONCLUSION] GET readiness and both POST contract checks passed. Inspect the server terminal for MAF and tool calls.'

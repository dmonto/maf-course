#requires -Version 5.1
# Ask a natural-language question; the client picks the service, the server (MAF agent + tool) returns evidence.
param([Parameter(Mandatory,Position=0)][string]$Question,[string]$BaseUri,
    [ValidateSet('fixture','azure')][string]$Mode='azure')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Write-UnitTrace -Kind 'USER' -Text $Question
if (-not $BaseUri) {
    Assert-AzureContext
    $unit14App=Read-AzJson -Arguments @('webapp','show','-g',$env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME)
    $BaseUri='https://'+$unit14App.defaultHostName
}
# The server only knows these services; routing the free text is the client's job.
$unit14Service=if ($Question -match '(?i)vpn') { 'vpn' } elseif ($Question -match '(?i)e-?mail') { 'email' } else { $null }
if (-not $unit14Service) { throw 'This server can only answer about "vpn" or "email". Mention one of them in the question.' }
Write-UnitTrace -Kind 'ROUTE' -Text "Question mapped to service '$unit14Service' (client-side keyword match)."
$unit14Headers=@{}
if ($env:SMOKE_BEARER_TOKEN) { $unit14Headers.Authorization="Bearer $env:SMOKE_BEARER_TOKEN" }
$unit14Body=@{service=$unit14Service} | ConvertTo-Json -Compress
Write-UnitTrace -Kind 'HTTP REQUEST' -Text "POST $($BaseUri.TrimEnd('/'))/api/status"
Write-UnitTrace -Kind 'HTTP JSON BODY' -Text $unit14Body
$unit14Watch=[Diagnostics.Stopwatch]::StartNew()
$unit14Answer=Invoke-RestMethod -Method Post -Uri ($BaseUri.TrimEnd('/')+'/api/status') -ContentType 'application/json' `
    -Headers $unit14Headers -Body $unit14Body -TimeoutSec 120
Write-UnitTrace -Kind 'HTTP RESPONSE' -Text ("200 in {0} ms | mode={1} release={2} requestId={3}" -f $unit14Watch.ElapsedMilliseconds,$unit14Answer.mode,$unit14Answer.release,$unit14Answer.requestId)
if ($unit14Answer.mode -ne $Mode) { throw "Expected mode $Mode but the server answered in mode $($unit14Answer.mode)." }
Write-UnitTrace -Kind 'EVIDENCE' -Text ($unit14Answer | Select-Object service,status,summary | ConvertTo-Json -Compress)
Write-UnitTrace -Kind 'ANSWER' -Text "$($unit14Answer.service) is $($unit14Answer.status). $($unit14Answer.summary)"

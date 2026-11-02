$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Import-Env.ps1')
Get-Command foundry -ErrorAction Stop | Out-Null
if ([string]::IsNullOrWhiteSpace($env:FOUNDRY_LOCAL_MODEL)) {
    foundry model list
    throw 'Set FOUNDRY_LOCAL_MODEL in the root .env to the assigned full model ID, then retry.'
}
$api = [Uri]$env:FOUNDRY_LOCAL_BASE_URL
if (-not $api.IsLoopback -or $api.AbsolutePath.TrimEnd('/') -ne '/v1') {
    throw 'FOUNDRY_LOCAL_BASE_URL must be a loopback /v1/ URL.'
}
foundry --version
if ($LASTEXITCODE -ne 0) { throw 'Foundry Local is unavailable.' }
# Apply the same explicit port configured in the shared .env.
foundry server restart --port $api.Port --idle-timeout 0
if ($LASTEXITCODE -ne 0) { throw 'Foundry Local server startup failed.' }
foundry server status
if ($LASTEXITCODE -ne 0) { throw 'Foundry Local status check failed.' }
foundry model info $env:FOUNDRY_LOCAL_MODEL
if ($LASTEXITCODE -ne 0) { throw 'Model information lookup failed.' }
foundry model download $env:FOUNDRY_LOCAL_MODEL
if ($LASTEXITCODE -ne 0) { throw 'Model download failed.' }
foundry model load $env:FOUNDRY_LOCAL_MODEL
if ($LASTEXITCODE -ne 0) { throw 'Model load failed.' }
$runtimeRoot = $api.GetLeftPart([UriPartial]::Authority)
$modelsUrl = $env:FOUNDRY_LOCAL_BASE_URL.TrimEnd('/') + '/models'
$models = Invoke-RestMethod -Uri $modelsUrl -Method Get
$models | ConvertTo-Json -Depth 10
Write-Host 'Use data[].id as FOUNDRY_LOCAL_API_MODEL if it differs from the CLI variant ID.'
foundry model list --loaded --verbose
if ($LASTEXITCODE -ne 0) { throw 'Loaded-model inspection failed.' }

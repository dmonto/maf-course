$ErrorActionPreference = 'Stop'
$unit08Root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1')
foreach ($rawLine in Get-Content (Join-Path $unit08Root '.env')) {
    $line = $rawLine.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $split = $line.IndexOf('=')
    if ($split -le 0) { throw 'Use NAME=value in the shared .env.' }
    $key = $line.Substring(0, $split).Trim()
    if ($key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid dotenv key.' }
    $value = $line.Substring($split + 1).Trim()
    if ($value.Length -ge 2 -and
        (($value.StartsWith('"') -and $value.EndsWith('"')) -or
         ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    # Treat dotenv values as data. Do not dot-source or evaluate them.
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($key))) {
        [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
}
foreach ($key in 'TENANT_ID', 'SUBSCRIPTION_ID', 'AZURE_OPENAI_ENDPOINT', 'AZURE_OPENAI_DEPLOYMENT_NAME') {
    $value = [Environment]::GetEnvironmentVariable($key)
    if ([string]::IsNullOrWhiteSpace($value) -or $value.Contains('YOUR-') -or $value.Contains('<')) {
        throw "Set $key in the shared .env."
    }
}
& az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
& az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }

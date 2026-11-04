$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Import-Env.ps1')
if ([string]::IsNullOrWhiteSpace($env:TENANT_ID) -or [string]::IsNullOrWhiteSpace($env:SUBSCRIPTION_ID)) {
    throw 'Set TENANT_ID and SUBSCRIPTION_ID in the shared root .env.'
}
& az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in failed.' }
& az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }

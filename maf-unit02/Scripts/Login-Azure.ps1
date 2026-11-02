$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Import-Env.ps1')
if ($env:TENANT_ID) { az login --tenant $env:TENANT_ID } else { az login }
if ($LASTEXITCODE -ne 0) { throw 'Azure sign-in failed.' }
if ($env:SUBSCRIPTION_ID) {
    az account set --subscription $env:SUBSCRIPTION_ID
    if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }
}

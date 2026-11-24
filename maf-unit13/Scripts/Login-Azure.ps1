[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
foreach ($unit13Key in 'TENANT_ID','SUBSCRIPTION_ID','AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME','AZURE_OPENAI_APPROVED_HOSTS') {
    $unit13Value = [Environment]::GetEnvironmentVariable($unit13Key)
    if ([string]::IsNullOrWhiteSpace($unit13Value) -or $unit13Value.Contains('<')) {
        throw "Complete the assigned $unit13Key in the shared .env."
    }
}
& az login --tenant $env:TENANT_ID
if ($LASTEXITCODE -ne 0) { throw 'Azure login failed.' }
& az account set --subscription $env:SUBSCRIPTION_ID
if ($LASTEXITCODE -ne 0) { throw 'Subscription selection failed.' }

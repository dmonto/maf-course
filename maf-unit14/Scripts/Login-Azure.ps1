#requires -Version 5.1
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-Assigned -Names @('TENANT_ID','SUBSCRIPTION_ID')
$null=Invoke-UnitTool -Executable az -Arguments @('login','--tenant',$env:TENANT_ID)
$null=Invoke-UnitTool -Executable az -Arguments @('account','set','--subscription',$env:SUBSCRIPTION_ID)
Assert-AzureContext
Write-Host 'Assigned Azure CLI context selected.'

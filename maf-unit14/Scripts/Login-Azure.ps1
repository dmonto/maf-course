#requires -Version 5.1
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Write-UnitTrace -Kind 'MILESTONE' -Text 'Sign in to the assigned tenant and select the assigned subscription (a browser login may open).'
Assert-Assigned -Names @('TENANT_ID','SUBSCRIPTION_ID')
$null=Invoke-UnitTool -Executable az -Arguments @('login','--tenant',$env:TENANT_ID)
$null=Invoke-UnitTool -Executable az -Arguments @('account','set','--subscription',$env:SUBSCRIPTION_ID)
Assert-AzureContext
Write-UnitTrace -Kind 'CONCLUSION' -Text 'Assigned Azure CLI context selected.'

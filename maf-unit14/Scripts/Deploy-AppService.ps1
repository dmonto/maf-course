#requires -Version 5.1
param([ValidateSet('fixture','azure')][string]$Mode='fixture',[string]$ImageRef,[switch]$Apply)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $ImageRef) { $ImageRef=$env:APPROVED_IMAGE }
if (-not $Apply) {
    @{planOnly=$true;imageRef=$ImageRef;mode=$Mode;targetPort=8080;identity='managed';
      required='Assigned sitecontainers app, main container name main, ingress and registry/model roles'} | ConvertTo-Json
    return
}
Assert-ImageReference -Image $ImageRef
Assert-Assigned -Names @('RESOURCE_GROUP','WEBAPP_NAME')
Assert-AzureContext
$unit14Config=Read-AzJson -Arguments @('webapp','config','show','-g',$env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME)
if ($unit14Config.linuxFxVersion -ne 'sitecontainers') { throw 'Use the assigned sidecar-enabled Linux app.' }
$unit14Limits=Get-UnitRunLimits
$unit14Settings=@("Runner__Mode=$Mode",'Runner__Identity=managed','Runner__Concurrency=2',
    "Runner__RequestTimeoutSeconds=$($unit14Limits.timeout)","Runner__MaxOutputTokens=$($unit14Limits.tokens)")
if ($Mode -eq 'azure') {
    Assert-Assigned -Names @('AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME')
    $unit14Settings+=@("AZURE_OPENAI_ENDPOINT=$env:AZURE_OPENAI_ENDPOINT","AZURE_OPENAI_DEPLOYMENT_NAME=$env:AZURE_OPENAI_DEPLOYMENT_NAME")
}
$null=Invoke-UnitTool -Executable az -Arguments (@('webapp','config','appsettings','set','-g',$env:RESOURCE_GROUP,
    '-n',$env:WEBAPP_NAME,'--settings')+$unit14Settings)
$null=Invoke-UnitTool -Executable az -Arguments @('webapp','sitecontainers','create','-g',$env:RESOURCE_GROUP,
    '-n',$env:WEBAPP_NAME,'--container-name','main','--image',$ImageRef,'--target-port','8080',
    '--is-main','true','--system-assigned-identity','true')
$unit14Artifacts=New-UnitArtifactDirectory -Name 'appservice'
$unit14Health=Join-Path $unit14Artifacts 'health-config.json'
@{healthCheckPath='/health/ready'} | ConvertTo-Json | Set-UnitContent $unit14Health
$null=Invoke-UnitTool -Executable az -Arguments @('webapp','config','set','-g',$env:RESOURCE_GROUP,
    '-n',$env:WEBAPP_NAME,'--generic-configurations',"@$unit14Health")
$unit14App=Read-AzJson -Arguments @('webapp','show','-g',$env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME)
Write-Host "Applied image: $ImageRef"
Write-Host "Run Probe-Deployment.ps1 against https://$($unit14App.defaultHostName) in mode $Mode."

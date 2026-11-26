#requires -Version 5.1
param([Parameter(Mandatory)][ValidateSet('aks','appservice')][string]$Target,
    [Parameter(Mandatory)][string]$ProbeReport,[Parameter(Mandatory)][string]$ConfigRevision,
    [ValidateSet('fixture','azure')][string]$Mode='azure',[string]$Output)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$unit14Probe=Get-Content -LiteralPath $ProbeReport -Raw -Encoding UTF8 | ConvertFrom-UnitJson
if (-not $unit14Probe.passed -or $unit14Probe.mode -ne $Mode -or
    $unit14Probe.release -ne $env:APPROVED_RELEASE) { throw 'Use matching successful probe evidence for APPROVED_RELEASE and its actual mode.' }
$unit14Record=@{platform=$Target;release=$unit14Probe.release;mode=$Mode;configRevision=$ConfigRevision;
    probeReport=(Resolve-Path -LiteralPath $ProbeReport).Path;capturedAt=[DateTimeOffset]::UtcNow.ToString('O')}
Write-UnitTrace -Kind 'MILESTONE' -Text "Capture the approved-release recovery record for $Target (read-only queries)."
if ($Target -eq 'aks') {
    Assert-Assigned -Names @('KUBE_CONTEXT','KUBE_NAMESPACE')
    $unit14DeploymentText=@(Invoke-UnitTool -Executable kubectl -Arguments @('--context',$env:KUBE_CONTEXT,
        'get','deployment','maf-status','-n',$env:KUBE_NAMESPACE,'-o','json')) -join "`n"
    $unit14Deployment=$unit14DeploymentText | ConvertFrom-UnitJson
    $unit14Container=@($unit14Deployment.spec.template.spec.containers | Where-Object name -eq 'api')
    if ($unit14Container.Count -ne 1) { throw 'Expected one named API container.' }
    $unit14Record.imageRef=$unit14Container[0].image
    $unit14Record.context=$env:KUBE_CONTEXT
    $unit14Record.namespace=$env:KUBE_NAMESPACE
    $unit14Record.podTemplate=$unit14Deployment.spec.template
} else {
    Assert-AzureContext
    Assert-Assigned -Names @('RESOURCE_GROUP','WEBAPP_NAME')
    $unit14Container=Read-AzJson -Arguments @('webapp','sitecontainers','show','-g',$env:RESOURCE_GROUP,
        '-n',$env:WEBAPP_NAME,'--container-name','main')
    if ($unit14Container.ContainsKey('properties')) { $unit14Container=$unit14Container.properties }
    $unit14Record.imageRef=$unit14Container.image
    $unit14Record.resourceGroup=$env:RESOURCE_GROUP
    $unit14Record.webApp=$env:WEBAPP_NAME
    $unit14AllSettings=Read-AzJson -Arguments @('webapp','config','appsettings','list','-g',$env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME)
    $unit14Record.settings=@{}
    $unit14Names=@('Runner__Mode','Runner__Identity','Runner__Concurrency','Runner__RequestTimeoutSeconds','Runner__MaxOutputTokens','AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME')
    foreach ($unit14Item in $unit14AllSettings) {
        if ($unit14Item.name -in $unit14Names) { $unit14Record.settings[$unit14Item.name]=$unit14Item.value }
    }
}
Assert-ImageReference -Image $unit14Record.imageRef
if (-not $Output) { $Output=Join-Path (New-UnitArtifactDirectory -Name 'approved-release') 'release.json' }
$unit14Record | ConvertTo-Json -Depth 35 | Set-UnitContent $Output
Write-UnitTrace -Kind 'CONCLUSION' -Text "Recovery record: $Output"

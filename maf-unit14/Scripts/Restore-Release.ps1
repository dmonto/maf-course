#requires -Version 5.1
param([Parameter(Mandatory)][string]$ReleaseRecord,[switch]$Apply)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$unit14Record=Get-Content -LiteralPath $ReleaseRecord -Raw -Encoding UTF8 | ConvertFrom-UnitJson
Assert-ImageReference -Image $unit14Record.imageRef
if (-not $unit14Record.configRevision -or $unit14Record.mode -notin @('fixture','azure')) { throw 'Incomplete recovery record.' }
if (-not $Apply) {
    @{planOnly=$true;platform=$unit14Record.platform;imageRef=$unit14Record.imageRef;
        release=$unit14Record.release;configRevision=$unit14Record.configRevision;requiresFinalFunctionalProbe=$true} | ConvertTo-Json
    return
}
if ($unit14Record.platform -eq 'aks') {
    Assert-Assigned -Names @('KUBE_CONTEXT','KUBE_NAMESPACE')
    if ($unit14Record.context -ne $env:KUBE_CONTEXT -or $unit14Record.namespace -ne $env:KUBE_NAMESPACE) {
        throw 'Recovery record target differs from the assigned Kubernetes target.'
    }
    $unit14Api=@($unit14Record.podTemplate.spec.containers | Where-Object name -eq 'api')
    if ($unit14Api.Count -ne 1 -or $unit14Api[0].image -ne $unit14Record.imageRef) { throw 'Snapshot image differs from the retained approved image.' }
    $unit14Artifacts=New-UnitArtifactDirectory -Name 'rollback'
    $unit14Patch=Join-Path $unit14Artifacts 'restore-template.json'
    # Restoring the full recorded pod template also restores this host's inline model mapping.
    $unit14Operations=@(@{op='replace';path='/spec/template';value=$unit14Record.podTemplate})
    ConvertTo-Json -InputObject $unit14Operations -Depth 35 |
        Set-UnitContent $unit14Patch
    $null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$env:KUBE_CONTEXT,'patch','deployment','maf-status',
        '-n',$env:KUBE_NAMESPACE,'--type','json','--patch-file',$unit14Patch)
    $null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$env:KUBE_CONTEXT,'rollout','status',
        'deployment/maf-status','-n',$env:KUBE_NAMESPACE,'--timeout=180s')
} elseif ($unit14Record.platform -eq 'appservice') {
    Assert-AzureContext
    if ($unit14Record.resourceGroup -ne $env:RESOURCE_GROUP -or $unit14Record.webApp -ne $env:WEBAPP_NAME) {
        throw 'Recovery record differs from the assigned App Service target.'
    }
    $unit14Names=@('Runner__Mode','Runner__Identity','Runner__Concurrency','Runner__RequestTimeoutSeconds','Runner__MaxOutputTokens','AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME')
    foreach ($unit14Key in $unit14Record.settings.Keys) {
        if ($unit14Key -notin $unit14Names) { throw 'Unexpected recovery setting.' }
    }
    $unit14Missing=@($unit14Names | Where-Object { -not $unit14Record.settings.ContainsKey($_) })
    if ($unit14Missing.Count -gt 0) {
        $null=Invoke-UnitTool -Executable az -Arguments (@('webapp','config','appsettings','delete','-g',
            $env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME,'--setting-names')+$unit14Missing)
    }
    $unit14Settings=@($unit14Record.settings.Keys | ForEach-Object { "$_=$($unit14Record.settings[$_])" })
    if ($unit14Settings.Count -gt 0) {
        $null=Invoke-UnitTool -Executable az -Arguments (@('webapp','config','appsettings','set','-g',
            $env:RESOURCE_GROUP,'-n',$env:WEBAPP_NAME,'--settings')+$unit14Settings)
    }
    $null=Invoke-UnitTool -Executable az -Arguments @('webapp','sitecontainers','update','-g',$env:RESOURCE_GROUP,
        '-n',$env:WEBAPP_NAME,'--container-name','main','--image',$unit14Record.imageRef)
} else { throw 'Unsupported recovery platform.' }
Write-Host "Run the functional probe for release $($unit14Record.release), mode $($unit14Record.mode), and retain the recovery report."

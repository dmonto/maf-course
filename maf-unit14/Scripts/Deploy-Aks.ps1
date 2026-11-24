#requires -Version 5.1
param([ValidateSet('fixture','azure')][string]$Mode='fixture',[string]$ImageRef,[switch]$WithHpa,[switch]$Apply)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $ImageRef) { $ImageRef=$env:APPROVED_IMAGE }
$unit14Result=Invoke-Section -Id v03
$unit14Manifest=Get-Content (Join-Path $unit14Result.artifacts 'kubernetes.json') -Raw -Encoding UTF8 | ConvertFrom-UnitJson
if ($ImageRef) { Assert-ImageReference -Image $ImageRef; $unit14Manifest.items[1].spec.template.spec.containers[0].image=$ImageRef }
$unit14Namespace=$unit14Manifest.items[1].metadata.namespace
$unit14Limits=Get-UnitRunLimits
if ($WithHpa) {
    $unit14ResultHpa=Invoke-Section -Id v04
    $unit14Hpa=Get-Content (Join-Path $unit14ResultHpa.artifacts 'hpa.json') -Raw -Encoding UTF8 | ConvertFrom-UnitJson
    $unit14Manifest.items[1].spec.Remove('replicas')
    $unit14Manifest.items+=@($unit14Hpa)
}
if ($Mode -eq 'azure') {
    Assert-Assigned -Names @('WORKLOAD_CLIENT_ID','AZURE_OPENAI_ENDPOINT','AZURE_OPENAI_DEPLOYMENT_NAME')
    $unit14Template=$unit14Manifest.items[1].spec.template
    $unit14Template.metadata.labels['azure.workload.identity/use']='true'
    $unit14Template.spec.serviceAccountName='maf-agent'
    $unit14Template.spec.containers[0].env=@(
        @{name='Runner__Mode';value='azure'},@{name='Runner__Identity';value='workload'},
        @{name='Runner__Concurrency';value='2'},
        @{name='Runner__RequestTimeoutSeconds';value="$($unit14Limits.timeout)"},
        @{name='Runner__MaxOutputTokens';value="$($unit14Limits.tokens)"},
        @{name='AZURE_OPENAI_ENDPOINT';value=$env:AZURE_OPENAI_ENDPOINT},
        @{name='AZURE_OPENAI_DEPLOYMENT_NAME';value=$env:AZURE_OPENAI_DEPLOYMENT_NAME})
    $unit14ServiceAccount=@{apiVersion='v1';kind='ServiceAccount';metadata=@{name='maf-agent';namespace=$unit14Namespace;
        annotations=@{'azure.workload.identity/client-id'=$env:WORKLOAD_CLIENT_ID}}}
    # Put the service account before the workload; federation remains an infrastructure prerequisite.
    $unit14Manifest.items=@($unit14Manifest.items[0],$unit14ServiceAccount)+@($unit14Manifest.items | Select-Object -Skip 1)
}
$unit14Artifacts=New-UnitArtifactDirectory -Name 'aks'
$unit14Path=Join-Path $unit14Artifacts 'deployment.json'
$unit14Manifest | ConvertTo-Json -Depth 35 | Set-UnitContent $unit14Path
Write-Host "Manifest: $unit14Path"
if (-not $Apply) { Write-Host 'Plan only. No Kubernetes operation was executed.'; return }
Assert-ImageReference -Image $ImageRef
Assert-Assigned -Names @('KUBE_CONTEXT')
$null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$env:KUBE_CONTEXT,'apply','-f',$unit14Path)
$null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$env:KUBE_CONTEXT,'rollout','status',
    'deployment/maf-status','-n',$unit14Namespace,'--timeout=180s')
Write-Host "Use kubectl --context $env:KUBE_CONTEXT port-forward service/maf-status 8081:80 -n $unit14Namespace and probe it in mode $Mode."

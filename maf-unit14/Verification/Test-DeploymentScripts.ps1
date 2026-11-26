#requires -Version 5.1
param([switch]$InternalChild)
# Command-contract checks only. Azure, Podman and Kubernetes are replaced by local functions.
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scripts/Compatibility.ps1')
if (-not $InternalChild) {
    # Keep simulated commands and environment values out of the caller's interactive session.
    $unit14Shell=Get-UnitShellPath
    & $unit14Shell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -InternalChild
    if ($LASTEXITCODE -ne 0) { throw 'Simulated deployment command checks failed.' }
    return
}
$unit14TestRoot=Split-Path -Parent $PSScriptRoot
$global:Unit14Calls=[System.Collections.Generic.List[object]]::new()
$global:Unit14Applied=$null
$global:Unit14Patch=$null
$global:Unit14Image='fixture.azurecr.io/maf-status@sha256:' + ('a' * 64)
$env:TENANT_ID='fixture-tenant'
$env:SUBSCRIPTION_ID='fixture-subscription'
$env:RESOURCE_GROUP='fixture-group'
$env:WEBAPP_NAME='fixture-app'
$env:ACR_NAME='fixture'
$env:KUBE_CONTEXT='fixture-context'
$env:KUBE_NAMESPACE='maf-lab'
$env:WORKLOAD_CLIENT_ID='00000000-0000-0000-0000-000000000001'
$env:AZURE_OPENAI_ENDPOINT='https://fixture.openai.azure.com/'
$env:AZURE_OPENAI_DEPLOYMENT_NAME='fixture-model'
$env:APPROVED_IMAGE=$global:Unit14Image
$env:APPROVED_RELEASE='14.1.0'
$env:Runner__Release='14.1.0'

function Assert-Test([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Get-TestArgument([object[]]$Values,[string]$Name) {
    $unit14Index=[Array]::IndexOf($Values,$Name)
    if ($unit14Index -lt 0) { throw "Missing argument $Name." }
    return $Values[$unit14Index+1]
}
function global:az {
    $global:LASTEXITCODE=0
    $unit14NativeArgs=@($args)
    $global:Unit14Calls.Add(@{tool='az';args=$unit14NativeArgs})
    $unit14Command=$unit14NativeArgs -join ' '
    if ($unit14Command.StartsWith('account show')) { @{id=$env:SUBSCRIPTION_ID;tenantId=$env:TENANT_ID} | ConvertTo-Json -Compress; return }
    if ($unit14Command.StartsWith('webapp config show')) { @{linuxFxVersion='sitecontainers'} | ConvertTo-Json -Compress; return }
    if ($unit14Command.StartsWith('webapp sitecontainers show')) { @{properties=@{image=$global:Unit14Image}} | ConvertTo-Json -Compress; return }
    if ($unit14Command.StartsWith('webapp config appsettings list')) {
        @(@{name='Runner__Mode';value='azure'},@{name='Runner__Identity';value='managed'},
          @{name='Runner__Concurrency';value='2'},@{name='AZURE_OPENAI_ENDPOINT';value=$env:AZURE_OPENAI_ENDPOINT},
          @{name='AZURE_OPENAI_DEPLOYMENT_NAME';value='fixture-model'}) | ConvertTo-Json -Compress; return
    }
    if ($unit14Command.StartsWith('webapp config set')) {
        $unit14File=Get-TestArgument $unit14NativeArgs '--generic-configurations'
        Assert-Test $unit14File.StartsWith('@') 'Configuration must use a JSON file.'
        $unit14Config=Get-Content $unit14File.Substring(1) -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-Test ($unit14Config.healthCheckPath -eq '/health/ready') 'Health path was lost.'
        '{}'; return
    }
    if ($unit14Command.StartsWith('webapp show')) { @{defaultHostName='fixture.example.invalid'} | ConvertTo-Json -Compress; return }
    if ($unit14Command.StartsWith('webapp config appsettings set') -or
        $unit14Command.StartsWith('webapp config appsettings delete') -or
        $unit14Command.StartsWith('webapp sitecontainers create') -or
        $unit14Command.StartsWith('webapp sitecontainers update')) { '{}'; return }
    if ($unit14Command.StartsWith('acr show')) { @{loginServer='fixture.azurecr.io'} | ConvertTo-Json -Compress; return }
    if ($unit14Command.StartsWith('acr login')) {
        Assert-Test ($unit14NativeArgs -contains '--expose-token') 'ACR login must not invoke a default engine.'
        @{loginServer='fixture.azurecr.io';accessToken='unit14-synthetic-token'} | ConvertTo-Json -Compress; return
    }
    if ($unit14Command.StartsWith('acr repository show')) { @{digest=('sha256:' + ('a' * 64))} | ConvertTo-Json -Compress; return }
    throw "Unexpected simulated Azure operation: $unit14Command"
}
function global:podman {
    $global:LASTEXITCODE=0
    $unit14NativeArgs=@($args)
    $global:Unit14Calls.Add(@{tool='podman';args=$unit14NativeArgs})
    if ($unit14NativeArgs[0] -eq 'info') {
        if ($global:Unit14EngineFails) { $global:LASTEXITCODE=1; return }
        '{"host":{"arch":"amd64"}}'; return
    }
    if ($unit14NativeArgs[0] -eq 'build') {
        $global:Unit14EmbeddedRelease=(Get-TestArgument $unit14NativeArgs '--build-arg').Substring('RELEASE_VERSION='.Length)
        'simulated build'; return
    }
    if (($unit14NativeArgs -join ' ').StartsWith('image inspect')) {
        if ($global:Unit14WrongImageRelease) { 'Runner__Release=old-release' }
        else { "Runner__Release=$global:Unit14EmbeddedRelease" }
        return
    }
    if ($unit14NativeArgs[0] -eq 'login') {
        $unit14Input=@($input)
        Assert-Test ($unit14NativeArgs -contains '--password-stdin') 'Token must use stdin.'
        Assert-Test ($unit14NativeArgs -notcontains 'unit14-synthetic-token') 'Token leaked into arguments.'
        Assert-Test ($unit14Input.Count -eq 1 -and $unit14Input[0] -eq 'unit14-synthetic-token') 'Unexpected stdin token.'
        $global:Unit14TokenSent=$true
        if ($global:Unit14LoginFails) { $global:LASTEXITCODE=1; return }
        'simulated login'; return
    }
    'simulated podman operation'
}
function global:kubectl {
    $global:LASTEXITCODE=0
    $unit14NativeArgs=@($args)
    $global:Unit14Calls.Add(@{tool='kubectl';args=$unit14NativeArgs})
    if ($unit14NativeArgs -contains 'apply') {
        $unit14File=Get-TestArgument $unit14NativeArgs '-f'
        $global:Unit14Applied=Get-Content $unit14File -Raw -Encoding UTF8 | ConvertFrom-UnitJson
        'simulated apply'; return
    }
    if ($unit14NativeArgs -contains 'get') {
        $unit14Deployment=$global:Unit14Applied.items | Where-Object kind -eq 'Deployment'
        $unit14Deployment | ConvertTo-Json -Depth 35 -Compress; return
    }
    if ($unit14NativeArgs -contains 'patch') {
        $unit14File=Get-TestArgument $unit14NativeArgs '--patch-file'
        $global:Unit14Patch=Get-Content $unit14File -Raw -Encoding UTF8 | ConvertFrom-UnitJson
        'simulated patch'; return
    }
    if ($unit14NativeArgs -contains 'rollout') { 'simulated ready'; return }
    throw 'Unexpected simulated kubectl operation.'
}

& (Join-Path $unit14TestRoot 'Scripts/Deploy-AppService.ps1') -Mode fixture | Out-Null
Assert-Test ($global:Unit14Calls.Count -eq 0) 'Plan-only App Service execution called a cloud CLI.'
& (Join-Path $unit14TestRoot 'Scripts/Deploy-AppService.ps1') -Mode azure -Apply
$unit14Create=@($global:Unit14Calls | Where-Object { $_.tool -eq 'az' -and ($_.args -join ' ').StartsWith('webapp sitecontainers create') })[-1]
Assert-Test ((Get-TestArgument $unit14Create.args '--image') -eq $global:Unit14Image) 'Immutable App Service image was changed.'
Assert-Test ((Get-TestArgument $unit14Create.args '--target-port') -eq '8080') 'App Service target port changed.'

& (Join-Path $unit14TestRoot 'Scripts/Deploy-Aks.ps1') -Mode azure -WithHpa -Apply
$unit14Deployment=$global:Unit14Applied.items | Where-Object kind -eq 'Deployment'
Assert-Test (-not $unit14Deployment.spec.ContainsKey('replicas')) 'Fixed replica count conflicts with the HPA.'
Assert-Test ($unit14Deployment.spec.template.spec.serviceAccountName -eq 'maf-agent') 'Workload service account missing.'
Assert-Test ($unit14Deployment.spec.template.metadata.labels['azure.workload.identity/use'] -eq 'true') 'Workload identity label missing.'
Assert-Test (@($global:Unit14Applied.items | Where-Object kind -eq 'HorizontalPodAutoscaler').Count -eq 1) 'HPA was not included.'
Assert-Test (@($global:Unit14Applied.items | Where-Object kind -eq 'ServiceAccount').Count -eq 1) 'ServiceAccount was not included.'
& (Join-Path $unit14TestRoot 'Scripts/Deploy-Aks.ps1') -Mode azure -UseExistingNamespace -Apply
Assert-Test (@($global:Unit14Applied.items | Where-Object kind -eq 'Namespace').Count -eq 0) 'Namespace-scoped deployment tried to modify the Namespace.'
Assert-Test (@($global:Unit14Applied.items | Where-Object kind -eq 'Deployment').Count -eq 1) 'Namespace filtering removed the workload.'

$unit14TestArtifacts=Join-Path $unit14TestRoot ('artifacts/script-checks/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $unit14TestArtifacts -Force | Out-Null
$unit14Probe=Join-Path $unit14TestArtifacts 'simulated-probe.json'
@{passed=$true;release='14.1.0';mode='azure';simulated=$true} | ConvertTo-Json | Set-UnitContent $unit14Probe
foreach ($unit14Platform in @('aks','appservice')) {
    $unit14Record=Join-Path $unit14TestArtifacts "$unit14Platform-release.json"
    & (Join-Path $unit14TestRoot 'Scripts/Capture-Release.ps1') -Target $unit14Platform -ProbeReport $unit14Probe -ConfigRevision fixture-config -Output $unit14Record
    & (Join-Path $unit14TestRoot 'Scripts/Restore-Release.ps1') -ReleaseRecord $unit14Record -Apply
}
Assert-Test ($global:Unit14Patch.Count -eq 1) 'Expected one JSON Patch operation.'
Assert-Test ($global:Unit14Patch[0].op -eq 'replace' -and $global:Unit14Patch[0].path -eq '/spec/template') 'Pod template was not restored atomically.'
$unit14Restored=$global:Unit14Patch[0].value.spec.containers | Where-Object name -eq 'api'
Assert-Test (($unit14Restored.env | Where-Object name -eq 'AZURE_OPENAI_DEPLOYMENT_NAME').value -eq 'fixture-model') 'Original inline model mapping was not retained.'
& (Join-Path $unit14TestRoot 'Scripts/Build-Container.ps1') -PushImage
Assert-Test $global:Unit14TokenSent 'Registry authentication did not use token stdin.'
$unit14Build=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'build' })[-1]
Assert-Test ((Get-TestArgument $unit14Build.args '--file').EndsWith('Containerfile')) 'Wrong build recipe.'
Assert-Test ((Get-TestArgument $unit14Build.args '-t') -eq 'localhost/maf-status:14.1.0') 'Short-name local image used.'
$unit14Push=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'push' })[-1]
Assert-Test ((Get-TestArgument $unit14Push.args '--format') -eq 'v2s2') 'Image format changed.'
$unit14Published=& (Join-Path $unit14TestRoot 'Scripts/Build-Container.ps1') -Release '14.1.2-check' -NoCache -PushImage -PassThru
Assert-Test ($unit14Published.release -eq '14.1.2-check' -and $unit14Published.imageRef -eq $global:Unit14Image) 'Publish result did not retain the exact release and immutable reference.'
$unit14Build=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'build' })[-1]
Assert-Test ($unit14Build.args -contains '--no-cache' -and
    (Get-TestArgument $unit14Build.args '--build-arg') -eq 'RELEASE_VERSION=14.1.2-check') 'Explicit release did not override a stale environment or bypass the cache.'
Assert-Test ($unit14Build.args[-1] -eq $unit14TestRoot) 'Build context must follow all build options.'
$unit14PriorPushes=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'push' }).Count
$global:Unit14WrongImageRelease=$true
$unit14Rejected=$false
try { & (Join-Path $unit14TestRoot 'Scripts/Build-Container.ps1') -PushImage }
catch { $unit14Rejected=$_.Exception.Message.StartsWith('Built image release differs') }
Assert-Test $unit14Rejected 'An image with the wrong embedded release was published.'
Assert-Test (@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'push' }).Count -eq $unit14PriorPushes) 'Push ran after image validation failed.'
$global:Unit14WrongImageRelease=$false
$unit14PriorPushes=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'push' }).Count
$global:Unit14LoginFails=$true
$unit14Rejected=$false
try { & (Join-Path $unit14TestRoot 'Scripts/Build-Container.ps1') -PushImage }
catch { $unit14Rejected=$_.Exception.Message -eq 'Podman registry authentication failed.' }
Assert-Test $unit14Rejected 'Registry login failure was ignored.'
Assert-Test (@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'push' }).Count -eq $unit14PriorPushes) 'Image pushed after failed authentication.'
$global:Unit14EngineFails=$true
$unit14PriorBuilds=@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'build' }).Count
$unit14Rejected=$false
try { & (Join-Path $unit14TestRoot 'Scripts/Build-Container.ps1') }
catch { $unit14Rejected=$_.Exception.Message.StartsWith('Podman is not ready.') }
Assert-Test $unit14Rejected 'Unavailable engine was ignored.'
Assert-Test (@($global:Unit14Calls | Where-Object { $_.tool -eq 'podman' -and $_.args[0] -eq 'build' }).Count -eq $unit14PriorBuilds) 'Build started without a ready engine.'
@{passed=$true;simulation=$true;azureExecuted=$false;podmanExecuted=$false;kubernetesExecuted=$false;
  checks=@('plan-only boundary','sitecontainers identity and port','health JSON file','AKS workload identity','HPA ownership','release capture','full pod-template recovery','App Service settings recovery','registry digest arguments','Podman Containerfile and local tag','ACR token via stdin','authentication failure blocks push','engine readiness blocks build')} |
    ConvertTo-Json -Depth 8 | Set-UnitContent (Join-Path $unit14TestArtifacts 'verification.json')
Write-Host 'ALL SIMULATED DEPLOYMENT SCRIPT CHECKS PASSED'

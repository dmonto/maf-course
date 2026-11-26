#requires -Version 5.1
param([switch]$InternalChild)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scripts/Compatibility.ps1')
if (-not $InternalChild) {
    & (Get-UnitShellPath) -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -InternalChild
    if ($LASTEXITCODE -ne 0) { throw 'Session deployment boundary checks failed.' }
    return
}
$unit14TestRoot=Split-Path -Parent $PSScriptRoot
$env:Runner__Release='14.1.2'
$env:KUBE_CONTEXT='do-not-use-cloud-context'
$env:KIND_EXPERIMENTAL_PROVIDER='original-provider'
$global:Unit14Calls=[System.Collections.Generic.List[object]]::new()
function Assert-Test([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function global:podman {
    $global:LASTEXITCODE=0
    $global:Unit14Calls.Add(@{tool='podman';args=@($args)})
    if ($args[0] -eq 'info') { '{}'; return }
    if ($args[0] -eq 'save') {
        $unit14OutputIndex=[Array]::IndexOf(@($args),'--output')
        'synthetic archive' | Set-Content -LiteralPath $args[$unit14OutputIndex+1]
        return
    }
    throw 'Unexpected simulated Podman operation.'
}
function global:kind {
    $global:LASTEXITCODE=0
    Assert-Test ($env:KIND_EXPERIMENTAL_PROVIDER -eq 'podman') 'Kind used a different container engine.'
    $global:Unit14Calls.Add(@{tool='kind';args=@($args)})
    if ($args[0] -eq 'get') { 'maf-unit14'; return }
    if ($args[0] -eq 'load') {
        if ($global:Unit14LoadFails) { $global:LASTEXITCODE=5 }
        return
    }
    throw 'Unexpected simulated Kind operation.'
}
function global:kubectl {
    $global:LASTEXITCODE=0
    $global:Unit14Calls.Add(@{tool='kubectl';args=@($args)})
    Assert-Test ($args[0] -eq '--context' -and $args[1] -eq 'kind-maf-unit14') 'Local deployment could target the cloud context.'
    if ($args -contains 'apply') {
        $unit14FileIndex=[Array]::IndexOf(@($args),'-f')
        $global:Unit14LocalManifest=Get-Content -LiteralPath $args[$unit14FileIndex+1] -Raw | ConvertFrom-UnitJson
    }
}
& (Join-Path $unit14TestRoot 'Scripts/Deploy-Kind.ps1')
Assert-Test ($global:Unit14Calls.Count -eq 0) 'Plan-only local deployment invoked an engine or cluster.'
& (Join-Path $unit14TestRoot 'Scripts/Deploy-Kind.ps1') -Apply
$unit14Deployment=$global:Unit14LocalManifest.items | Where-Object kind -eq 'Deployment'
Assert-Test ($unit14Deployment.metadata.namespace -eq 'maf-local') 'Local deployment used a cloud namespace.'
Assert-Test ($unit14Deployment.spec.template.spec.containers[0].image -eq 'localhost/maf-status:14.1.2' -and
    $unit14Deployment.spec.template.spec.containers[0].imagePullPolicy -eq 'Never') 'Kind did not use the loaded local image.'
Assert-Test (($unit14Deployment.spec.template.spec.containers[0].env | Where-Object name -eq 'Runner__Mode').value -eq 'fixture') 'Local Kubernetes unexpectedly enabled model inference.'
Assert-Test ($env:KIND_EXPERIMENTAL_PROVIDER -eq 'original-provider') 'Kind provider was not restored.'
$global:Unit14Calls.Clear()
$global:Unit14LoadFails=$true
$unit14Rejected=$false
try { & (Join-Path $unit14TestRoot 'Scripts/Deploy-Kind.ps1') -Apply } catch { $unit14Rejected=$true }
Assert-Test ($unit14Rejected -and @($global:Unit14Calls | Where-Object tool -eq 'kubectl').Count -eq 0) 'Apply ran after image import failed.'
Assert-Test ($env:KIND_EXPERIMENTAL_PROVIDER -eq 'original-provider') 'Failure leaked the Kind provider setting.'
$global:Unit14HealthCalls=0
function global:Invoke-RestMethod {
    param($Uri,$Headers,$TimeoutSec,$MaximumRedirection,$ErrorAction)
    $global:Unit14HealthCalls++
    if ($global:Unit14HealthCalls -eq 1) { return @{status='ok';release='14.1.1';mode='azure';promptVersion='status-v1'} }
    return @{status='ok';release='14.1.2';mode='azure';promptVersion='status-v1';deadlinePolicy='bounded-wait-v1'}
}
$unit14Health=& (Join-Path $unit14TestRoot 'Scripts/Wait-Deployment.ps1') -BaseUri https://fixture.invalid -Mode azure -Release 14.1.2 -TimeoutSeconds 5
Assert-Test ($global:Unit14HealthCalls -eq 2 -and $unit14Health.release -eq '14.1.2') 'Rollout wait did not reject old health and accept the new release.'
$global:Unit14HealthCalls=0
$unit14Rejected=$false
try { & (Join-Path $unit14TestRoot 'Scripts/Wait-Deployment.ps1') -BaseUri http://remote.invalid -Mode azure }
catch { $unit14Rejected=$true }
Assert-Test ($unit14Rejected -and $global:Unit14HealthCalls -eq 0) 'Remote HTTP was accepted by the rollout wait.'
Write-Host 'ALL SESSION DEPLOYMENT BOUNDARY CHECKS PASSED'

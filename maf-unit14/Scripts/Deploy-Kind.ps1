#requires -Version 5.1
param([ValidatePattern('^[a-z0-9][a-z0-9-]{0,39}$')][string]$ClusterName='maf-unit14',
    [ValidatePattern('^[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?$')][string]$Namespace='maf-local',
    [switch]$Apply)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$unit14Release=$env:Runner__Release
if ($unit14Release -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { throw 'Invalid release tag.' }
$unit14Context="kind-$ClusterName"
$unit14Image="localhost/maf-status:$unit14Release"
$unit14Result=Invoke-Section -Id v03
$unit14Manifest=Get-Content (Join-Path $unit14Result.artifacts 'kubernetes.json') -Raw -Encoding UTF8 | ConvertFrom-UnitJson
foreach ($unit14Item in $unit14Manifest.items) {
    if ($unit14Item.kind -eq 'Namespace') { $unit14Item.metadata.name=$Namespace }
    else { $unit14Item.metadata.namespace=$Namespace }
    if ($unit14Item.kind -eq 'Deployment') {
        $unit14Item.spec.template.spec.containers[0].image=$unit14Image
        $unit14Item.spec.template.spec.containers[0].imagePullPolicy='Never'
    }
}
$unit14Artifacts=New-UnitArtifactDirectory -Name 'kind'
$unit14ManifestPath=Join-Path $unit14Artifacts 'deployment.json'
$unit14Manifest | ConvertTo-Json -Depth 35 | Set-UnitContent $unit14ManifestPath
Write-UnitTrace -Kind 'MILESTONE' -Text "Manifest: $unit14ManifestPath"
Write-UnitTrace -Kind 'MILESTONE' -Text "Local context: $unit14Context; namespace: $Namespace; mode: fixture"
if (-not $Apply) { Write-UnitTrace -Kind 'CONCLUSION' -Text 'Plan only. No Podman, Kind or Kubernetes operation was executed. Add -Apply to run it.'; return }
Write-UnitTrace -Kind 'MILESTONE' -Text 'Apply to Kind: Podman is the provider; export the image, load it into the node, apply, wait.'
Assert-PodmanReady
if (-not (Get-Command kind -ErrorAction SilentlyContinue)) { throw 'Install the Kind CLI through Podman Desktop and reopen the terminal.' }
$unit14PreviousProvider=$env:KIND_EXPERIMENTAL_PROVIDER
try {
    $env:KIND_EXPERIMENTAL_PROVIDER='podman'
    Write-UnitTrace -Kind 'ENV' -Text 'KIND_EXPERIMENTAL_PROVIDER=podman'
    $unit14Clusters=@(Invoke-UnitTool -Executable kind -Arguments @('get','clusters'))
    if ($unit14Clusters -notcontains $ClusterName) { throw "Create the local Kind cluster $ClusterName before applying." }
    # The Podman image store is separate from the containerd image store inside Kind nodes.
    $unit14Archive=Join-Path $unit14Artifacts 'image.tar'
    $null=Invoke-UnitTool -Executable podman -Arguments @('save','--format','docker-archive','--output',$unit14Archive,$unit14Image)
    $null=Invoke-UnitTool -Executable kind -Arguments @('load','image-archive',$unit14Archive,'--name',$ClusterName)
    $null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$unit14Context,'apply','-f',$unit14ManifestPath)
    $null=Invoke-UnitTool -Executable kubectl -Arguments @('--context',$unit14Context,'rollout','status','deployment/maf-status',
        '-n',$Namespace,'--timeout=180s')
} finally { $env:KIND_EXPERIMENTAL_PROVIDER=$unit14PreviousProvider }
Write-UnitTrace -Kind 'CONCLUSION' -Text "Rollout complete. Forward with: kubectl --context $unit14Context -n $Namespace port-forward service/maf-status 8082:80"
Write-UnitTrace -Kind 'NEXT' -Text 'Probe http://127.0.0.1:8082 in fixture mode. This path does not invoke Azure OpenAI.'

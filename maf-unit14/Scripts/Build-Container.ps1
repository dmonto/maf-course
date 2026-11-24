#requires -Version 5.1
param([switch]$PushImage)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-PodmanReady
$unit14Release=$env:Runner__Release
if ($unit14Release -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { throw 'Invalid release tag.' }
$unit14LocalImage="localhost/maf-status:$unit14Release"
# Select the v2 image format for the existing Azure hosting targets; the engine is Podman.
$null=Invoke-UnitTool -Executable podman -Arguments @('build','--format','docker',
    '--file',(Join-Path $unit14Root 'Containerfile'),'--build-arg',"RELEASE_VERSION=$unit14Release",
    '-t',$unit14LocalImage,$unit14Root)
if ($PushImage) {
    Assert-AzureContext
    Assert-Assigned -Names @('ACR_NAME')
    $unit14Registry=Read-AzJson -Arguments @('acr','show','--name',$env:ACR_NAME)
    $unit14Login=Read-AzJson -Arguments @('acr','login','--name',$env:ACR_NAME,'--expose-token')
    if (-not $unit14Login.accessToken -or $unit14Login.loginServer -ne $unit14Registry.loginServer) {
        throw 'ACR did not return a token for the assigned registry.'
    }
    try {
        # Pass the short-lived token through stdin, never through arguments or an artifact.
        $unit14Login.accessToken | & podman login $unit14Registry.loginServer `
            --username '00000000-0000-0000-0000-000000000000' --password-stdin | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Podman registry authentication failed.' }
    } finally { $unit14Login=$null }
    $unit14Tag="$($unit14Registry.loginServer)/maf-status:$unit14Release"
    $null=Invoke-UnitTool -Executable podman -Arguments @('tag',$unit14LocalImage,$unit14Tag)
    $null=Invoke-UnitTool -Executable podman -Arguments @('push','--format','v2s2',$unit14Tag)
    $unit14Remote=Read-AzJson -Arguments @('acr','repository','show','--name',$env:ACR_NAME,'--image',"maf-status:$unit14Release")
    $unit14Image="$($unit14Registry.loginServer)/maf-status@$($unit14Remote.digest)"
    Assert-ImageReference -Image $unit14Image
    $unit14Artifacts=New-UnitArtifactDirectory -Name 'image'
    @{release=$unit14Release;imageRef=$unit14Image;source='ACR registry lookup';engine='podman'} | ConvertTo-Json |
        Set-UnitContent (Join-Path $unit14Artifacts 'image.json')
    Write-Host "Image reference: $unit14Image"
}

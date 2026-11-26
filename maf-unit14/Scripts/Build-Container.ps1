#requires -Version 5.1
param([switch]$PushImage,[string]$Release,[switch]$NoCache,[switch]$PassThru)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-PodmanReady
$unit14Release=if ($Release) { $Release } else { $env:Runner__Release }
if ($unit14Release -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { throw 'Invalid release tag.' }
$unit14LocalImage="localhost/maf-status:$unit14Release"
# Select the v2 image format for the existing Azure hosting targets; the engine is Podman.
$unit14BuildArguments=@('build','--format','docker',
    '--file',(Join-Path $unit14Root 'Containerfile'),'--build-arg',"RELEASE_VERSION=$unit14Release",
    '-t',$unit14LocalImage)
if ($NoCache) { $unit14BuildArguments+='--no-cache' }
$unit14BuildArguments+=$unit14Root
Write-UnitTrace -Kind 'MILESTONE' -Text "Build release $unit14Release as $unit14LocalImage with Podman (docker image format for Azure hosts)."
$null=Invoke-UnitTool -Executable podman -Arguments $unit14BuildArguments
Write-UnitTrace -Kind 'MILESTONE' -Text 'Inspect the built image to confirm the embedded Runner__Release.'
# Inspect only the release value; do not print the image's complete environment.
$unit14Embedded=@(Invoke-UnitTool -Executable podman -Arguments @('image','inspect',$unit14LocalImage,
    '--format','{{range .Config.Env}}{{println .}}{{end}}') | Where-Object { $_ -like 'Runner__Release=*' })
if ($unit14Embedded.Count -ne 1 -or $unit14Embedded[0] -cne "Runner__Release=$unit14Release") {
    throw 'Built image release differs from the requested release. Publication was stopped.'
}
$unit14Evidence=[pscustomobject]@{release=$unit14Release;localImage=$unit14LocalImage;imageRef=$null;engine='podman'}
if ($PushImage) {
    Write-UnitTrace -Kind 'MILESTONE' -Text 'Push: resolve the ACR, log Podman in with a short-lived token (stdin, never printed), tag and push.'
    Assert-AzureContext
    Assert-Assigned -Names @('ACR_NAME')
    $unit14Registry=Read-AzJson -Arguments @('acr','show','--name',$env:ACR_NAME)
    $unit14Login=Read-AzJson -Arguments @('acr','login','--name',$env:ACR_NAME,'--expose-token')
    if (-not $unit14Login.accessToken -or $unit14Login.loginServer -ne $unit14Registry.loginServer) {
        throw 'ACR did not return a token for the assigned registry.'
    }
    try {
        # Pass the short-lived token through stdin, never through arguments or an artifact.
        Write-UnitTrace -Kind 'COMMAND' -Text "podman login $($unit14Registry.loginServer) --username 00000000-0000-0000-0000-000000000000 --password-stdin   (token piped on stdin, hidden)"
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
    Write-UnitTrace -Kind 'CONCLUSION' -Text "Immutable image reference (digest from the ACR): $unit14Image"
    $unit14Evidence.imageRef=$unit14Image
}
if (-not $PushImage) { Write-UnitTrace -Kind 'CONCLUSION' -Text "Local image $unit14LocalImage built and verified. Nothing was pushed." }
if ($PassThru) { Write-Output $unit14Evidence }

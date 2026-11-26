#requires -Version 5.1
param([switch]$KeepContainer)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-PodmanReady
$unit14Name='maf-status-14-' + [guid]::NewGuid().ToString('N').Substring(0,10)
$unit14Image="localhost/maf-status:$env:Runner__Release"
$unit14Started=$false
try {
    Write-UnitTrace -Kind 'MILESTONE' -Text "Start $unit14Image as $unit14Name (read-only, loopback port chosen by Podman) and probe it."
    $null=Invoke-UnitTool -Executable podman -Arguments @('run','--detach','--name',$unit14Name,
        '--read-only','--tmpfs','/tmp','-p','127.0.0.1::8080',$unit14Image)
    $unit14Started=$true
    $unit14Ports=@(Invoke-UnitTool -Executable podman -Arguments @('port',$unit14Name,'8080/tcp'))
    $unit14Binding=@($unit14Ports | Where-Object { $_ -match '^127\.0\.0\.1:\d+$' } | Select-Object -First 1)
    if ($unit14Binding.Count -ne 1) { throw 'Cannot identify the Podman loopback port.' }
    & (Join-Path $PSScriptRoot 'Probe-Deployment.ps1') -BaseUri "http://$($unit14Binding[0])" `
        -Release $env:Runner__Release -Mode fixture
} finally {
    if ($unit14Started) {
        Write-UnitTrace -Kind 'COMMAND' -Text "podman logs $unit14Name --tail 20"
        & podman logs $unit14Name --tail 20
        if (-not $KeepContainer) { Write-UnitTrace -Kind 'COMMAND' -Text "podman rm --force $unit14Name"; & podman rm --force $unit14Name | Out-Null }
        else { Write-UnitTrace -Kind 'CONCLUSION' -Text "Retained container: $unit14Name" }
    }
}

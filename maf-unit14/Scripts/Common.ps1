. (Join-Path $PSScriptRoot 'Compatibility.ps1')
. (Join-Path $PSScriptRoot 'Import-Environment.ps1')
function Invoke-UnitTool {
    param([string]$Executable,[string[]]$Arguments)
    $unit14Output = & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE." }
    return $unit14Output
}
function Read-AzJson {
    param([string[]]$Arguments)
    $unit14Text = @(Invoke-UnitTool -Executable az -Arguments ($Arguments + @('--output','json'))) -join "`n"
    return $unit14Text | ConvertFrom-UnitJson
}
function Assert-Assigned {
    param([string[]]$Names)
    foreach ($unit14Name in $Names) {
        $unit14Value=[Environment]::GetEnvironmentVariable($unit14Name)
        if ([string]::IsNullOrWhiteSpace($unit14Value) -or $unit14Value.Contains('<')) {
            throw "Load the assigned $unit14Name in the shared .env."
        }
    }
}
function Assert-AzureContext {
    Assert-Assigned -Names @('TENANT_ID','SUBSCRIPTION_ID')
    $unit14Account=Read-AzJson -Arguments @('account','show')
    if ($unit14Account.id -ne $env:SUBSCRIPTION_ID -or $unit14Account.tenantId -ne $env:TENANT_ID) {
        throw 'Azure CLI tenant/subscription differs from the assigned .env. Run Login-Azure.ps1.'
    }
}
function Assert-ImageReference {
    param([string]$Image)
    if ($Image -cnotmatch '^[a-z0-9][a-z0-9.-]*\.azurecr\.io/[a-z0-9][a-z0-9._/-]*@sha256:[0-9a-f]{64}$') {
        throw 'Use an immutable ACR image reference including @sha256 and its 64 lowercase hexadecimal digits.'
    }
}
function New-UnitArtifactDirectory {
    param([string]$Name)
    $unit14Path=Join-Path $unit14Root ("artifacts/manual/$Name-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $unit14Path -Force | Out-Null
    return $unit14Path
}
function Invoke-Section {
    param([string]$Id)
    $unit14Examples=Get-Content (Join-Path $unit14Root 'examples.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $unit14Example=$unit14Examples | Where-Object id -eq $Id
    if (-not $unit14Example) { throw 'Unknown section id.' }
    $unit14Output=@(Invoke-UnitTool -Executable dotnet -Arguments @('run','--project',
        (Join-Path $unit14Root $unit14Example.project),'--no-build','--no-launch-profile','--','--mode','fixture'))
    $unit14Result=$unit14Output | Where-Object { $_ -is [string] -and $_.StartsWith('RESULT ') } | Select-Object -Last 1
    if (-not $unit14Result) { throw 'Section did not produce a result.' }
    return $unit14Result.Substring(7) | ConvertFrom-Json
}


function Assert-PodmanReady {
    if (-not (Get-Command podman -ErrorAction SilentlyContinue)) {
        throw 'Install Podman, reopen the terminal and run Initialize-Podman.ps1.'
    }
    try { $null=Invoke-UnitTool -Executable podman -Arguments @('info','--format','json') }
    catch { throw 'Podman is not ready. Run Initialize-Podman.ps1 or check the configured Podman connection.' }
}

function Get-UnitRunLimits {
    $unit14Timeout=90
    $unit14Tokens=2048
    if ($env:Runner__RequestTimeoutSeconds -and
        -not [int]::TryParse($env:Runner__RequestTimeoutSeconds,[ref]$unit14Timeout)) {
        throw 'Runner__RequestTimeoutSeconds must be 1..180.'
    }
    if ($unit14Timeout -lt 1 -or $unit14Timeout -gt 180) { throw 'Runner__RequestTimeoutSeconds must be 1..180.' }
    if ($env:Runner__MaxOutputTokens -and
        -not [int]::TryParse($env:Runner__MaxOutputTokens,[ref]$unit14Tokens)) {
        throw 'Runner__MaxOutputTokens must be 128..8192.'
    }
    if ($unit14Tokens -lt 128 -or $unit14Tokens -gt 8192) { throw 'Runner__MaxOutputTokens must be 128..8192.' }
    return @{timeout=$unit14Timeout;tokens=$unit14Tokens}
}

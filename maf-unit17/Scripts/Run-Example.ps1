param([Parameter(Mandatory=$true)][ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08','v09','v10')][string]$Example,[switch]$NoBuild,[string[]]$ExampleArgs=@())
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $project = Join-Path $Example "$Example.csproj"
    $dotnetArgs = @('run','--project',$project)
    if ($NoBuild) { $dotnetArgs += '--no-build' }
    $dotnetArgs += '--'
    $dotnetArgs += $ExampleArgs
    Write-Host ('[COMMAND] dotnet ' + ($dotnetArgs -join ' '))
    & dotnet @dotnetArgs
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "Example $Example exited with code $code" }
} finally { Pop-Location }

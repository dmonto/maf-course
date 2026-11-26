$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if (-not (Test-Path .env)) { Copy-Item .env.example .env }
    foreach ($n in 1..10) {
        $v = 'v{0:d2}' -f $n
        $project = Join-Path $v "$v.csproj"
        Write-Host "[COMMAND] dotnet build $project"
        & dotnet build $project
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $v" }
    }
    Write-Host '[CONCLUSION] All ten .NET projects built.'
} finally { Pop-Location }

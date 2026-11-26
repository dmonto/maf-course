[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $unitRoot 'Maf.Unit16.slnx'
Push-Location $unitRoot
try {
    Write-Host '[MILESTONE] Restore the pinned .NET 10 solution.'
    Write-Host ('[COMMAND] dotnet restore "{0}" --locked-mode -m:1' -f $solution)
    & dotnet restore $solution --locked-mode '-m:1'
    if ($LASTEXITCODE -ne 0) { throw 'Unit 16 restore failed.' }
    Write-Host ('[COMMAND] dotnet build "{0}" --no-restore -m:1' -f $solution)
    & dotnet build $solution --no-restore '-m:1'
    if ($LASTEXITCODE -ne 0) { throw 'Unit 16 build failed.' }
    Write-Host '[CONCLUSION] All Unit 16 C# projects built successfully.'
} finally { Pop-Location }

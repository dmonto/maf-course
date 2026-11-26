$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Helpers.ps1')
$root = Get-Unit15Root
$environmentFile = Join-Path $root '.env'
Write-Host "[MILESTONE] Ensure $environmentFile exists (copied from .env.example only when missing)."
if (-not (Test-Path -LiteralPath $environmentFile)) {
    Copy-Item -LiteralPath (Join-Path $root '.env.example') -Destination $environmentFile
}
Import-Unit15Environment
Write-Host '[CONCLUSION] Unit 15 environment is ready. Existing values were preserved.'

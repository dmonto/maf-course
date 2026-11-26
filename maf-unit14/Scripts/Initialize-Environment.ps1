#requires -Version 5.1
$ErrorActionPreference='Stop'
$unit14Root=Split-Path -Parent $PSScriptRoot
$unit14File=Join-Path $unit14Root '.env'
if (-not (Test-Path -LiteralPath $unit14File)) {
    Copy-Item (Join-Path $unit14Root '.env.example') $unit14File
}
Write-Host "[MILESTONE] Ensure $unit14File exists (copied from .env.example only when missing)."
Write-Host '[CONCLUSION] Shared .env is ready. Existing values were preserved.'

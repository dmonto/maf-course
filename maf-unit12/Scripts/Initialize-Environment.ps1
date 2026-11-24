[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $unitRoot '.env'
if (-not (Test-Path -LiteralPath $target)) {
    Copy-Item -LiteralPath (Join-Path $unitRoot '.env.example') -Destination $target
    Write-Host 'Created the shared .env from placeholders.'
} else {
    Write-Host 'Preserved the existing shared .env.'
}
Write-Host 'Edit the one .env in maf-unit12. Process environment variables take precedence.'

[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
$environmentFile = Join-Path $unitRoot '.env'
if (-not (Test-Path -LiteralPath $environmentFile)) {
    Copy-Item -LiteralPath (Join-Path $unitRoot '.env.example') -Destination $environmentFile
    Write-Host 'Created the common .env from the placeholder template.'
} else {
    Write-Host 'The existing common .env was preserved.'
}

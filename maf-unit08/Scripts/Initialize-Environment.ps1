$ErrorActionPreference = 'Stop'
$unit08Root = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $unit08Root '.env'
if (-not (Test-Path $envPath)) {
    Copy-Item (Join-Path $unit08Root '.env.example') $envPath
    Write-Host 'Created the shared .env. Edit it before using live mode.'
}
else { Write-Host 'The shared .env already exists; it was preserved.' }

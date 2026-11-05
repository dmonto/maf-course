$ErrorActionPreference = 'Stop'
$unit07Root = Split-Path $PSScriptRoot -Parent
$envPath = Join-Path $unit07Root '.env'
if (-not (Test-Path $envPath)) {
    Copy-Item (Join-Path $unit07Root '.env.example') $envPath
    Write-Host 'Created the shared .env. Edit it before using live mode.'
}
else { Write-Host 'The shared .env already exists; it was preserved.' }

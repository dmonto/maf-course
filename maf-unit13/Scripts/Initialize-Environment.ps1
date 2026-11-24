[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$unit13Root = Split-Path -Parent $PSScriptRoot
$unit13Environment = Join-Path $unit13Root '.env'
if (-not (Test-Path -LiteralPath $unit13Environment)) {
    Copy-Item -LiteralPath (Join-Path $unit13Root '.env.example') -Destination $unit13Environment
}
Write-Output 'The shared .env is ready. Existing values were preserved.'

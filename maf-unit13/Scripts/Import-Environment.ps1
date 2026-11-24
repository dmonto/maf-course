# Dot-source this script. All values are literal; no expression is evaluated.
$unit13Root = Split-Path -Parent $PSScriptRoot
$unit13EnvFile = Join-Path $unit13Root '.env'
if (-not (Test-Path -LiteralPath $unit13EnvFile)) { throw 'Initialize the shared .env first.' }
foreach ($unit13RawLine in Get-Content -LiteralPath $unit13EnvFile) {
    $unit13Line = $unit13RawLine.Trim()
    if (-not $unit13Line -or $unit13Line.StartsWith('#')) { continue }
    $unit13Split = $unit13Line.IndexOf('=')
    if ($unit13Split -lt 1) { throw 'Invalid .env assignment.' }
    $unit13Key = $unit13Line.Substring(0, $unit13Split).Trim()
    $unit13Value = $unit13Line.Substring($unit13Split + 1).Trim()
    if ($unit13Key -notmatch '^[A-Z][A-Z0-9_]*$') { throw 'Invalid .env variable name.' }
    if ($unit13Value.Length -ge 2) {
        if (($unit13Value[0] -eq '"' -and $unit13Value[-1] -eq '"') -or
            ($unit13Value[0] -eq "'" -and $unit13Value[-1] -eq "'")) {
            $unit13Value = $unit13Value.Substring(1, $unit13Value.Length - 2)
        }
    }
    if ($unit13Value -and -not [Environment]::GetEnvironmentVariable($unit13Key)) {
        [Environment]::SetEnvironmentVariable($unit13Key, $unit13Value, 'Process')
    }
}

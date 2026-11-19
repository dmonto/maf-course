$ErrorActionPreference = 'Stop'
$unitRoot = Split-Path $PSScriptRoot -Parent
$environmentFile = Join-Path $unitRoot '.env'
if (-not (Test-Path -LiteralPath $environmentFile)) {
    Copy-Item -LiteralPath (Join-Path $unitRoot '.env.example') -Destination $environmentFile
}
foreach ($raw in [System.IO.File]::ReadAllLines($environmentFile)) {
    $line = $raw.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
    $equal = $line.IndexOf('=')
    if ($equal -lt 1) { throw 'Use NAME=value assignments in the common .env.' }
    $name = $line.Substring(0, $equal).Trim()
    if ($name -notmatch '^[A-Z][A-Z0-9_]*$') { throw 'Invalid environment variable name.' }
    $value = $line.Substring($equal + 1).Trim()
    if ($value.Length -ge 2) {
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
    }
    # Nonempty process settings have precedence over the file.
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

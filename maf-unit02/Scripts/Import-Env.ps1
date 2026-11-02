$ErrorActionPreference = 'Stop'
$courseRoot = Split-Path $PSScriptRoot -Parent
foreach ($raw in Get-Content (Join-Path $courseRoot '.env')) {
    $line = $raw.Trim()
    if (-not $line -or $line.StartsWith('#')) { continue }
    $split = $line.IndexOf('=')
    if ($split -le 0) { throw 'Use NAME=value in the shared .env file.' }
    $name = $line.Substring(0, $split).Trim()
    if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid setting name.' }
    $value = $line.Substring($split + 1).Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

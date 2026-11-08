# Dot-source this file to make the common .env available to Azure CLI commands.
$unit09EnvRoot = Split-Path $PSScriptRoot -Parent
$unit09EnvFile = Join-Path $unit09EnvRoot '.env'
if (-not (Test-Path $unit09EnvFile)) { throw 'Copy .env.example to .env and fill the assigned values.' }
foreach ($unit09EnvLine in Get-Content $unit09EnvFile) {
    $unit09EnvLine = $unit09EnvLine.Trim()
    if (-not $unit09EnvLine -or $unit09EnvLine.StartsWith('#')) { continue }
    $unit09EnvParts = $unit09EnvLine.Split(@('='), 2)
    if ($unit09EnvParts.Count -ne 2) { throw 'Invalid .env assignment.' }
    $unit09EnvKey = $unit09EnvParts[0].Trim()
    $unit09EnvValue = $unit09EnvParts[1].Trim()
    if ($unit09EnvKey -notmatch '^[A-Z][A-Z0-9_]*$') { throw 'Invalid .env variable name.' }
    if ($unit09EnvValue.Length -ge 2) {
        if (($unit09EnvValue[0] -eq '"' -and $unit09EnvValue[-1] -eq '"') -or
            ($unit09EnvValue[0] -eq "'" -and $unit09EnvValue[-1] -eq "'")) {
            $unit09EnvValue = $unit09EnvValue.Substring(1, $unit09EnvValue.Length - 2)
        }
    }
    if ($unit09EnvValue -and -not [Environment]::GetEnvironmentVariable($unit09EnvKey)) {
        [Environment]::SetEnvironmentVariable($unit09EnvKey, $unit09EnvValue, 'Process')
    }
}

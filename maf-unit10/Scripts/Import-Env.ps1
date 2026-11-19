# Dot-source this script to load the one common Unit 10 .env into the current process.
$unit10EnvRoot = Split-Path $PSScriptRoot -Parent
$unit10EnvFile = Join-Path $unit10EnvRoot '.env'
if (-not (Test-Path $unit10EnvFile)) { throw 'Copy .env.example to .env before loading it.' }
foreach ($unit10EnvLine in Get-Content $unit10EnvFile) {
    $unit10EnvLine = $unit10EnvLine.Trim()
    if (-not $unit10EnvLine -or $unit10EnvLine.StartsWith('#')) { continue }
    # Split on the first '=' only; works in Windows PowerShell 5.1 and PowerShell 7.
    $unit10EnvSplit = $unit10EnvLine.IndexOf('=')
    if ($unit10EnvSplit -lt 1) { throw 'Invalid .env assignment.' }
    $unit10EnvKey = $unit10EnvLine.Substring(0, $unit10EnvSplit).Trim()
    $unit10EnvValue = $unit10EnvLine.Substring($unit10EnvSplit + 1).Trim()
    if ($unit10EnvKey -notmatch '^[A-Z][A-Z0-9_]*$') { throw 'Invalid .env variable name.' }
    if ($unit10EnvValue.Length -ge 2) {
        if (($unit10EnvValue[0] -eq '"' -and $unit10EnvValue[-1] -eq '"') -or
            ($unit10EnvValue[0] -eq "'" -and $unit10EnvValue[-1] -eq "'")) {
            $unit10EnvValue = $unit10EnvValue.Substring(1, $unit10EnvValue.Length - 2)
        }
    }
    if ($unit10EnvValue -and -not [Environment]::GetEnvironmentVariable($unit10EnvKey)) {
        [Environment]::SetEnvironmentVariable($unit10EnvKey, $unit10EnvValue, 'Process')
    }
}

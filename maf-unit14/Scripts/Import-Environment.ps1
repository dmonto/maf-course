# Dot-source this loader. Values are literal and existing nonempty process values win.
$unit14Root = Split-Path -Parent $PSScriptRoot
$unit14Env = Join-Path $unit14Root '.env'
if (-not (Test-Path -LiteralPath $unit14Env)) { throw 'Run Initialize-Environment.ps1 first.' }
foreach ($unit14Raw in Get-Content -LiteralPath $unit14Env -Encoding UTF8) {
    $unit14Line = $unit14Raw.Trim()
    if (-not $unit14Line -or $unit14Line.StartsWith('#')) { continue }
    $unit14Split = $unit14Line.IndexOf('=')
    if ($unit14Split -lt 1) { throw 'Invalid .env assignment.' }
    $unit14Key = $unit14Line.Substring(0,$unit14Split).Trim()
    $unit14Value = $unit14Line.Substring($unit14Split+1).Trim()
    if ($unit14Key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid .env name.' }
    if ($unit14Value.Length -ge 2 -and
        (($unit14Value[0] -eq '"' -and $unit14Value[-1] -eq '"') -or
         ($unit14Value[0] -eq "'" -and $unit14Value[-1] -eq "'"))) {
        $unit14Value = $unit14Value.Substring(1,$unit14Value.Length-2)
    }
    if ($unit14Value -and -not [Environment]::GetEnvironmentVariable($unit14Key)) {
        [Environment]::SetEnvironmentVariable($unit14Key,$unit14Value,'Process')
    }
}

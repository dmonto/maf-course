$ErrorActionPreference = 'Stop'
$unit03PackageRoot = Split-Path $PSScriptRoot -Parent
$unit03EnvPath = Join-Path $unit03PackageRoot '.env'
if (-not (Test-Path -LiteralPath $unit03EnvPath)) { throw 'Copy the root .env.example to .env first.' }
foreach ($unit03RawLine in Get-Content -LiteralPath $unit03EnvPath) {
    $unit03Line = $unit03RawLine.Trim()
    if (-not $unit03Line -or $unit03Line.StartsWith('#')) { continue }
    $unit03Parts = $unit03Line.Split(@('='), 2, [StringSplitOptions]::None)
    if ($unit03Parts.Count -ne 2) { throw 'Use NAME=value in the root .env.' }
    $unit03Name = $unit03Parts[0].Trim()
    if ($unit03Name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid variable name in .env.' }
    $unit03Value = $unit03Parts[1].Trim()
    if ($unit03Value.Length -ge 2 -and
        (($unit03Value.StartsWith('"') -and $unit03Value.EndsWith('"')) -or
         ($unit03Value.StartsWith("'") -and $unit03Value.EndsWith("'")))) {
        $unit03Value = $unit03Value.Substring(1, $unit03Value.Length - 2)
    }
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($unit03Name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($unit03Name, $unit03Value, 'Process')
    }
}

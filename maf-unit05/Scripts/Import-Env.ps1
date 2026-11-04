$ErrorActionPreference = 'Stop'
$unit05PackageRoot = Split-Path $PSScriptRoot -Parent
$unit05EnvPath = Join-Path $unit05PackageRoot '.env'
if (-not (Test-Path -LiteralPath $unit05EnvPath)) { throw 'Copy the root .env.example to .env first.' }
foreach ($unit05RawLine in Get-Content -LiteralPath $unit05EnvPath) {
    $unit05Line = $unit05RawLine.Trim()
    if (-not $unit05Line -or $unit05Line.StartsWith('#')) { continue }
    $unit05Parts = $unit05Line.Split(@('='), 2, [StringSplitOptions]::None)
    if ($unit05Parts.Count -ne 2) { throw 'Use NAME=value in the root .env.' }
    $unit05Name = $unit05Parts[0].Trim()
    if ($unit05Name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid variable name in .env.' }
    $unit05Value = $unit05Parts[1].Trim()
    if ($unit05Value.Length -ge 2 -and
        (($unit05Value.StartsWith('"') -and $unit05Value.EndsWith('"')) -or
         ($unit05Value.StartsWith("'") -and $unit05Value.EndsWith("'")))) {
        $unit05Value = $unit05Value.Substring(1, $unit05Value.Length - 2)
    }
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($unit05Name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($unit05Name, $unit05Value, 'Process')
    }
}

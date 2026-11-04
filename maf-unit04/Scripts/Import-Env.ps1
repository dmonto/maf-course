$ErrorActionPreference = 'Stop'
$unit04PackageRoot = Split-Path $PSScriptRoot -Parent
$unit04EnvPath = Join-Path $unit04PackageRoot '.env'
if (-not (Test-Path -LiteralPath $unit04EnvPath)) { throw 'Copy the root .env.example to .env first.' }
foreach ($unit04RawLine in Get-Content -LiteralPath $unit04EnvPath) {
    $unit04Line = $unit04RawLine.Trim()
    if (-not $unit04Line -or $unit04Line.StartsWith('#')) { continue }
    $unit04Parts = $unit04Line.Split(@('='), 2, [StringSplitOptions]::None)
    if ($unit04Parts.Count -ne 2) { throw 'Use NAME=value in the root .env.' }
    $unit04Name = $unit04Parts[0].Trim()
    if ($unit04Name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid variable name in .env.' }
    $unit04Value = $unit04Parts[1].Trim()
    if ($unit04Value.Length -ge 2 -and
        (($unit04Value.StartsWith('"') -and $unit04Value.EndsWith('"')) -or
         ($unit04Value.StartsWith("'") -and $unit04Value.EndsWith("'")))) {
        $unit04Value = $unit04Value.Substring(1, $unit04Value.Length - 2)
    }
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($unit04Name, 'Process'))) {
        [Environment]::SetEnvironmentVariable($unit04Name, $unit04Value, 'Process')
    }
}

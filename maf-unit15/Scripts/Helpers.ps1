# Compatible with Windows PowerShell 5.1 and PowerShell 7.
function Get-Unit15Root {
    return (Split-Path $PSScriptRoot -Parent)
}
function Import-Unit15Environment {
    $path = Join-Path (Get-Unit15Root) '.env'
    if (-not (Test-Path -LiteralPath $path)) { return }
    foreach ($raw in [System.IO.File]::ReadAllLines($path)) {
        $line = $raw.Trim().TrimStart([char]0xFEFF)
        if (-not $line -or $line.StartsWith('#')) { continue }
        if ($line.StartsWith('export ')) { $line = $line.Substring(7) }
        $separator = $line.IndexOf('=')
        if ($separator -le 0) { throw 'Invalid assignment in the common .env.' }
        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim()
        if ($key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw 'Invalid environment variable name.' }
        if ($value.Length -ge 2) {
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }
        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($key, 'Process'))) {
            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
}
function Invoke-Unit15Project {
    param([string]$Project, [string[]]$ApplicationArguments = @(), [switch]$NoBuild)
    $root = Get-Unit15Root
    Import-Unit15Environment
    Write-Host '[ENV] Values from the shared .env were loaded into this process (existing nonempty values win).'
    $nativeArguments = @('run', '--project', (Join-Path $root $Project))
    if ($NoBuild) { $nativeArguments += '--no-build' }
    $nativeArguments += '--'
    $nativeArguments += $ApplicationArguments
    Write-Host "[MILESTONE] Run $Project (stdout = didactic trace, stderr = structured JSON events)."
    Write-Host ('[COMMAND] dotnet ' + ($nativeArguments -join ' '))
    & dotnet @nativeArguments
    if ($LASTEXITCODE -ne 0) { throw "The project failed with exit code $LASTEXITCODE." }
    Write-Host "[EXIT] dotnet exit code 0"
}

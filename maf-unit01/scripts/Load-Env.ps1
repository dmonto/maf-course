# Dot-source this script to make the root .env settings available to Azure CLI.
$mafPackageRoot = Split-Path $PSScriptRoot -Parent
$mafEnvFile = Join-Path $mafPackageRoot '.env'

if (Test-Path -LiteralPath $mafEnvFile) {
    $mafLineNumber = 0
    foreach ($mafRawLine in Get-Content -LiteralPath $mafEnvFile -Encoding UTF8) {
        $mafLineNumber++
        $mafLine = $mafRawLine.Trim()
        if ($mafLine.Length -eq 0 -or $mafLine.StartsWith('#')) { continue }
        if ($mafLine.StartsWith('export ')) { $mafLine = $mafLine.Substring(7).TrimStart() }
        $mafSeparator = $mafLine.IndexOf('=')
        if ($mafSeparator -lt 1) { throw "Invalid root .env entry on line $mafLineNumber." }
        $mafName = $mafLine.Substring(0, $mafSeparator).Trim()
        $mafValue = $mafLine.Substring($mafSeparator + 1).Trim()
        if ($mafName -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
            throw "Invalid root .env name on line $mafLineNumber."
        }
        if ($mafValue.StartsWith('"') -or $mafValue.StartsWith("'")) {
            if ($mafValue.Length -lt 2 -or $mafValue[$mafValue.Length - 1] -ne $mafValue[0]) {
                throw "Unmatched root .env quotes on line $mafLineNumber."
            }
            $mafValue = $mafValue.Substring(1, $mafValue.Length - 2)
        }
        if ($null -eq [Environment]::GetEnvironmentVariable($mafName, 'Process')) {
            [Environment]::SetEnvironmentVariable($mafName, $mafValue, 'Process')
        }
    }
}

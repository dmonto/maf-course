[CmdletBinding()]
param([ValidateSet('v01','v02','v04','v05','v07','v09')][string]$Example = 'v07',
    [ValidateSet('baseline','candidate')][string]$Label)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unitRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $unitRoot "artifacts/$Example"
if ($Label) { $artifactRoot = Join-Path $artifactRoot $Label }
$store = Join-Path $artifactRoot 'judged'
if (-not (Test-Path -LiteralPath $store) -or @(Get-ChildItem -LiteralPath $store -File -Recurse).Count -eq 0) {
    throw 'No reporting-library scenarios exist here. Run the selected example in azure mode first.'
}
$output = Join-Path $artifactRoot 'quality-report.html'
Push-Location $unitRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Local aieval tool restore failed.' }
    & dotnet tool run aieval report --path $store --output $output
    if ($LASTEXITCODE -ne 0) { throw 'aieval report generation failed.' }
} finally { Pop-Location }
Write-Host "Quality report: $output"

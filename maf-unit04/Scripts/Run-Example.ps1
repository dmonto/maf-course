param(
    [string]$Example = 'v04',
    [switch]$Demo,
    [switch]$Live,
    [ValidateSet('v1','v2')][string]$PromptVersion,
    [ValidateRange(1,65535)][int]$Port = 5050,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
$unit04Root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content (Join-Path $unit04Root 'manifest.json') -Raw | ConvertFrom-Json
$match = @((@($manifest.examples) + @($manifest.exercises)) | Where-Object {
    $_.id -eq $Example -or $_.directory -eq $Example
})
if ($match.Count -ne 1) { throw 'Use v01 through v10, ex01 or ex02.' }
$project = Join-Path $unit04Root $match[0].project
$forwardArgs = @('run','--project',$project,'--no-launch-profile')
if ($NoBuild) { $forwardArgs += '--no-build' }
if ($Example -ne 'ex01') {
    $forwardArgs += @('--','--port',"$Port")
    if ($Demo) { $forwardArgs += '--demo' }
    if ($Live) { $forwardArgs += '--live' }
}
$previousPrompt = $env:Agent__PromptVersion
try {
    if ($PromptVersion) { $env:Agent__PromptVersion = $PromptVersion }
    & dotnet @forwardArgs
    if ($LASTEXITCODE -ne 0) { throw "Example $Example failed." }
}
finally {
    if ($null -eq $previousPrompt) { Remove-Item Env:Agent__PromptVersion -ErrorAction SilentlyContinue }
    else { $env:Agent__PromptVersion = $previousPrompt }
}

param(
    [string]$Example='v06',
    [switch]$Demo,
    [switch]$Live,
    [switch]$Injection,
    [ValidateSet('v1','v2')][string]$PromptVersion,
    [string]$InputText,
    [ValidateRange(1,3)][int]$Repeat=1,
    [switch]$NoBuild
)
$ErrorActionPreference='Stop'
if ($Demo -and $Live) { throw 'Select either -Demo or -Live.' }
$unit05Root=Split-Path $PSScriptRoot -Parent
$manifest=Get-Content (Join-Path $unit05Root 'manifest.json') -Raw | ConvertFrom-Json
$match=@((@($manifest.examples)+@($manifest.exercises)) | Where-Object { $_.id -eq $Example -or $_.directory -eq $Example })
if ($match.Count -ne 1) { throw 'Use v01 through v09, ex01 or ex02.' }
$forwardArgs=@('run','--project',(Join-Path $unit05Root $match[0].project))
if ($NoBuild) { $forwardArgs+='--no-build' }
$forwardArgs+='--'
if ($Demo) { $forwardArgs+='--demo' }
if ($Live) { $forwardArgs+='--live' }
if ($Injection) { $forwardArgs+='--injection' }
if ($PromptVersion) { $forwardArgs+=@('--prompt-version',$PromptVersion) }
if ($PSBoundParameters.ContainsKey('InputText')) { $forwardArgs+=@('--input',$InputText) }
if ($Repeat -ne 1) { $forwardArgs+=@('--repeat',"$Repeat") }
$runStarted = Get-Date
& dotnet @forwardArgs
$exit = $LASTEXITCODE
if ($exit -ne 0) {
    $meaning = switch ($exit) {
        1 { 'execution failure (configuration, Azure call or unexpected exception)' }
        2 { 'contract-rejected (the model output failed validation, or a v2 case failed in a matrix run)' }
        default { 'unexpected exit code' }
    }
    $pv = if ($PromptVersion) { $PromptVersion } else { 'default (see PROMPT_VERSION in .env)' }
    Write-Host "Prompt version: $pv. Read the JSON 'outcome' and 'errors' fields printed above." -ForegroundColor Red
    if ($exit -eq 2 -and $PromptVersion -eq 'v1') {
        Write-Host 'v1 is the baseline prompt; contract rejections are expected to be recorded, not fixed. Compare with -PromptVersion v2.' -ForegroundColor Yellow
    }
    # Matrix examples (e.g. v09) save per-case rows; list the failing ones if this run wrote them.
    $resultsFile = Join-Path $unit05Root "output\$Example-results.json"
    if ((Test-Path $resultsFile) -and (Get-Item $resultsFile).LastWriteTime -ge $runStarted) {
        try {
            $rows = [object[]](Get-Content $resultsFile -Raw | ConvertFrom-Json)
            $bad = @($rows | Where-Object { $_.PSObject.Properties['checksPassed'] -and -not $_.checksPassed })
            if ($bad.Count -gt 0) {
                Write-Host "Failed cases ($($bad.Count) of $($rows.Count)); v2 failures set the exit code, v1 failures are baseline records:" -ForegroundColor Red
                foreach ($b in $bad) {
                    Write-Host ("  {0} {1,-18} {2}" -f $b.promptVersion, $b.case, ($b.failures -join ', ')) -ForegroundColor $(if ($b.promptVersion -eq 'v2') { 'Red' } else { 'DarkYellow' })
                }
                Write-Host "Details: $resultsFile" -ForegroundColor Red
                $v2bad = @($bad | Where-Object { $_.promptVersion -eq 'v2' })
                if ($v2bad.Count -gt 0) { $meaning = "$($v2bad.Count) candidate (v2) case(s) failed their checks; not an Azure or configuration error" }
            }
        } catch { Write-Host "Could not parse $resultsFile : $($_.Exception.Message)" -ForegroundColor DarkYellow }
    }
    Write-Host "Example $Example exited with code ${exit}: $meaning." -ForegroundColor Red
    $global:LASTEXITCODE = $exit
    throw "Example $Example failed (exit $exit): $meaning."
}

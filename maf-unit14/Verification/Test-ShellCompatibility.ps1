#requires -Version 5.1
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Scripts/Compatibility.ps1')
function Assert-Test([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
$unit14Value='{"items":[{"nested":[]},null,[{"ready":true}]],"empty":[],"single":[3],"name":"caf\u00e9"}' | ConvertFrom-UnitJson
Assert-Test ($unit14Value -is [System.Collections.IDictionary]) 'JSON objects must be mutable dictionaries.'
Assert-Test ($unit14Value.items.Count -eq 3 -and $null -eq $unit14Value.items[1]) 'Null array element was lost.'
Assert-Test ($unit14Value.items[0].nested.Count -eq 0 -and $unit14Value.items[2][0].ready) 'Nested arrays were changed.'
Assert-Test ($unit14Value.empty -is [array] -and $unit14Value.single.Count -eq 1) 'Empty/single array shape was lost.'
$unit14RootEmpty=ConvertFrom-UnitJson -Json '[]'
$unit14RootSingle=ConvertFrom-UnitJson -Json '[{"op":"replace"}]'
Assert-Test ($unit14RootEmpty -is [array] -and $unit14RootEmpty.Count -eq 0) 'Empty root array was lost.'
Assert-Test ($unit14RootSingle -is [array] -and $unit14RootSingle[0].op -eq 'replace') 'Single root array was unwrapped.'
$unit14Operations=@(@{op='replace';path='/spec/template';value=@{containers=@(@{name='api'})}})
$unit14Patch=ConvertTo-Json -InputObject $unit14Operations -Depth 35
Assert-Test ($unit14Patch.TrimStart().StartsWith('[')) 'JSON Patch must remain an array with one operation.'
$unit14Temp=Join-Path ([IO.Path]::GetTempPath()) ('unit14 shell ' + [guid]::NewGuid().ToString('N'))
$unit14OriginalValue=[Environment]::GetEnvironmentVariable('UNIT14_COMPAT_UTF8')
try {
    New-Item -ItemType Directory -Path (Join-Path $unit14Temp 'Scripts') -Force | Out-Null
    $unit14File=Join-Path $unit14Temp 'utf8 evidence.json'
    $unit14Value | ConvertTo-Json -Depth 35 | Set-UnitContent $unit14File
    $unit14Bytes=[IO.File]::ReadAllBytes($unit14File)
    Assert-Test (-not ($unit14Bytes[0] -eq 239 -and $unit14Bytes[1] -eq 187 -and $unit14Bytes[2] -eq 191)) 'Native JSON has an unexpected UTF-8 BOM.'
    $unit14Read=Get-Content -LiteralPath $unit14File -Raw -Encoding UTF8 | ConvertFrom-UnitJson
    Assert-Test ($unit14Read.name -eq ('caf'+[char]0x00e9)) 'UTF-8 text was changed.'
    $unit14Loader=Join-Path (Split-Path -Parent $PSScriptRoot) 'Scripts/Import-Environment.ps1'
    Copy-Item -LiteralPath $unit14Loader -Destination (Join-Path $unit14Temp 'Scripts/Import-Environment.ps1')
    ('UNIT14_COMPAT_UTF8=caf'+[char]0x00e9) | Set-UnitContent (Join-Path $unit14Temp '.env')
    [Environment]::SetEnvironmentVariable('UNIT14_COMPAT_UTF8',$null,'Process')
    . (Join-Path $unit14Temp 'Scripts/Import-Environment.ps1')
    Assert-Test ($env:UNIT14_COMPAT_UTF8 -eq ('caf'+[char]0x00e9)) 'The environment loader used the system code page.'
    $unit14Shell=Get-UnitShellPath
    Assert-Test (Test-Path -LiteralPath $unit14Shell) 'Child shell path is unavailable.'
    $unit14Accepted=Test-UnitTool -Executable $unit14Shell -Arguments @('-NoProfile','-Command',"[Console]::Error.WriteLine('expected unavailable engine'); exit 23")
    Assert-Test (-not $unit14Accepted) 'Expected native failure must return false instead of terminating setup.'
} finally {
    [Environment]::SetEnvironmentVariable('UNIT14_COMPAT_UTF8',$unit14OriginalValue,'Process')
    Remove-Item -LiteralPath $unit14Temp -Recurse -Force
}
Write-Host "ALL SHELL COMPATIBILITY CHECKS PASSED ($($PSVersionTable.PSVersion))"

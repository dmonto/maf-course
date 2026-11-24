[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$unitRoot = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $PSScriptRoot 'Run-Example.ps1'
$cases = @(
    @{ Id='v01'; Exit=0 }, @{ Id='v02'; Exit=0 }, @{ Id='v03'; Exit=0 },
    @{ Id='v04'; Exit=1 }, @{ Id='v05'; Exit=0 }, @{ Id='v06'; Exit=1 },
    @{ Id='v07'; Exit=0 }, @{ Id='v08'; Exit=0 }, @{ Id='v09'; Exit=0 },
    @{ Id='ex01'; Variant='bad'; Exit=1 }, @{ Id='ex01'; Variant='fixed'; Exit=0 },
    @{ Id='ex02'; Variant='bad'; Exit=1 }, @{ Id='ex02'; Variant='fixed'; Exit=0 }
)
foreach ($case in $cases) {
    $runParams = @{ Example=$case.Id; Mode='fixture'; Configuration=$Configuration; NoBuild=$NoBuild }
    if ($case.Variant) { $runParams.Variant = $case.Variant }
    & $runner @runParams
    if ($LASTEXITCODE -ne $case.Exit) { throw "Unexpected exit for $($case.Id): $LASTEXITCODE, expected $($case.Exit)." }
}
$verifyArgs = @('run','--project',(Join-Path $unitRoot 'Verification/Unit12.Checks.csproj'),'--configuration',$Configuration)
if ($NoBuild) { $verifyArgs += '--no-build' }
& dotnet @verifyArgs
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
& (Join-Path $PSScriptRoot 'Test-FixtureGate.ps1') -Configuration $Configuration -NoBuild:$NoBuild
Write-Host 'RESULT: all examples,  verification and the process gate passed.'

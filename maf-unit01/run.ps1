param(
    [Parameter(Position = 0)]
    [ValidateSet('v01', 'exercise02')]
    [string]$Project = 'v01',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Question = @()
)

$ErrorActionPreference = 'Stop'
$mafProjectPath = switch ($Project) {
    'v01' { Join-Path $PSScriptRoot 'v01-native-integration/Unit01.Intro.csproj' }
    'exercise02' { Join-Path $PSScriptRoot 'exercise-solutions/ex02-evidence-limit/Unit01.Exercise02.csproj' }
}

# Absolute project paths allow this script to be called from any working directory.
& dotnet run --project $mafProjectPath -- @Question
exit $LASTEXITCODE

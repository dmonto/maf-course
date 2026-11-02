param(
    [ValidateSet('v01','v02','v03','v04','v05','v06','v07','v08')][string]$Example = 'v05',
    [switch]$Demo,
    [ValidateSet('','azure','foundry','foundry-local')][string]$Provider = '',
    [string]$Prompt = ''
)
$ErrorActionPreference = 'Stop'
$courseRoot = Split-Path $PSScriptRoot -Parent
$folder = Get-ChildItem $courseRoot -Directory -Filter "$Example-*" | Select-Object -First 1
if (-not $folder) { throw 'Example directory not found.' }
$project = Get-ChildItem $folder.FullName -Filter '*.csproj' | Select-Object -First 1
$arguments = @()
if ($Demo) { $arguments += '--demo' }
if ($Provider) { $arguments += @('--provider', $Provider) }
if ($Prompt) { $arguments += @('--prompt', $Prompt) }
dotnet run --project $project.FullName -- @arguments
if ($LASTEXITCODE -ne 0) { throw "Example $Example failed." }

$ErrorActionPreference = 'Stop'
$courseRoot = Split-Path $PSScriptRoot -Parent
dotnet restore (Join-Path $courseRoot 'Maf.Unit02.slnx') --locked-mode -m:1
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet build (Join-Path $courseRoot 'Maf.Unit02.slnx') --no-restore -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
dotnet run --no-build --project (Join-Path $courseRoot 'Verification/Unit02.Checks.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
foreach ($folder in Get-ChildItem $courseRoot -Directory -Filter 'v??-*') {
    $project = Get-ChildItem $folder.FullName -Filter '*.csproj' | Select-Object -First 1
    dotnet run --no-build --project $project.FullName -- --demo
    if ($LASTEXITCODE -ne 0) { throw "Demo failed: $($folder.Name)" }
}
foreach ($folder in Get-ChildItem (Join-Path $courseRoot 'Exercises') -Directory) {
    $project = Get-ChildItem $folder.FullName -Filter '*.csproj' | Select-Object -First 1
    dotnet run --no-build --project $project.FullName -- --demo
    if ($LASTEXITCODE -ne 0) { throw "Exercise demo failed: $($folder.Name)" }
}
Write-Host 'Build, checks and all synthetic demonstrations passed.'

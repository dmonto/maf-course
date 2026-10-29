# Compatible with Windows PowerShell 5.1 and PowerShell 7.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-CheckedTool {
    param([string]$Name, [string[]]$ToolArgs)
    try { $output = & $Name @ToolArgs 2>&1; $code = $LASTEXITCODE }
    catch { throw "Cannot run $Name. Check installation, PATH and corporate execution policy." }
    if ($code -ne 0) {
        throw "$Name failed (exit $code). Check installation, connectivity and sign-in; contact the coordinator if it continues."
    }
    return ($output -join [Environment]::NewLine)
}

try {
    Set-Location $PSScriptRoot
    foreach ($tool in @("git", "dotnet", "az", "docker")) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is missing from PATH. Install it before the course." }
    }
    $sdk = Invoke-CheckedTool "dotnet" @("--version")
    if ($sdk -notmatch '^10\.') { throw "The repository requires a stable .NET 10 SDK. Check dotnet --list-sdks and global.json." }
    $null = Invoke-CheckedTool "git" @("--version")
    $null = Invoke-CheckedTool "az" @("version", "--output", "json")
    try { $null = Invoke-CheckedTool "docker" @("info", "--format", "{{.ServerVersion}}") }
    catch { throw "Docker daemon is unavailable. Start Docker Desktop/Engine and check your user permissions." }
    Write-Host "[PASS] Local tools: Git, .NET 10 SDK, Azure CLI and Docker daemon."

    if ([string]::IsNullOrWhiteSpace($env:TENANT_ID) -or [string]::IsNullOrWhiteSpace($env:SUBSCRIPTION_ID)) {
        throw "Load your edited configuration first: . .\student.env.ps1"
    }
    try { $account = (Invoke-CheckedTool "az" @("account", "show", "--output", "json")) | ConvertFrom-Json }
    catch { throw "Azure sign-in is unavailable. Run az login --tenant `$env:TENANT_ID, then az account set --subscription `$env:SUBSCRIPTION_ID." }
    if ($account.tenantId -ne $env:TENANT_ID -or $account.id -ne $env:SUBSCRIPTION_ID) {
        throw "Azure CLI is using a different tenant or subscription. Repeat the sign-in and subscription-selection steps."
    }
    if ($account.state -ne "Enabled") { throw "Your assigned subscription is not enabled. Contact the coordinator." }
    if ($account.user.type -ne "user") { throw "Sign in with your assigned student user; a service-principal session does not verify student permissions." }
    Write-Host "[PASS] Azure CLI context: assigned user, tenant and subscription."

    try { $null = Invoke-CheckedTool "dotnet" @("build", "$PSScriptRoot/StudentSmokeTest/StudentSmokeTest.csproj", "--no-restore", "--nologo") }
    catch { throw "Build failed. Run dotnet restore .\StudentSmokeTest\StudentSmokeTest.csproj --locked-mode, then rerun. Check the .NET SDK and NuGet/proxy settings if it continues." }
    & dotnet run --project "$PSScriptRoot/StudentSmokeTest/StudentSmokeTest.csproj" --no-build --no-restore
    $result = $LASTEXITCODE
    if ($result -notin @(0, 1, 2)) {
        Write-Host "[FAIL] Startup: check .NET 10 and rerun the restore command in README.md."
        Write-Host "FINAL RESULT: ACTION REQUIRED"
        exit 2
    }
    exit $result
}
catch {
    Write-Host "[FAIL] Preflight: $($_.Exception.Message)"
    Write-Host "FINAL RESULT: ACTION REQUIRED"
    Write-Host "If any check fails, send the PASS/FAIL lines to the course coordinator. Do not send tokens or secrets."
    exit 2
}

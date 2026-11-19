function Invoke-UnitProject {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [string[]]$ApplicationArguments = @(),
        [switch]$NoBuild
    )
    $ErrorActionPreference = 'Stop'
    $dotnetArguments = [System.Collections.Generic.List[string]]::new()
    $dotnetArguments.Add('run')
    $dotnetArguments.Add('--project')
    $dotnetArguments.Add($Project)
    if ($NoBuild) { $dotnetArguments.Add('--no-build') }
    $dotnetArguments.Add('--')
    foreach ($argument in $ApplicationArguments) { $dotnetArguments.Add($argument) }
    & dotnet @dotnetArguments
    if ($LASTEXITCODE -ne 0) { throw 'The selected project failed. Inspect its RESULT error.' }
}

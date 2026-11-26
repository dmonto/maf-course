# Shared helpers for Windows PowerShell 5.1 and PowerShell Core.
$unit14OnWindows=[Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$unit14OnMacOS=$PSVersionTable.OS -match 'Darwin'

# Didactic console trace, same [KIND] style as Unit 16. Host output never pollutes captured command output.
function Write-UnitTrace {
    param([Parameter(Mandatory)][string]$Kind,[Parameter(Mandatory)][string]$Text)
    Write-Host ("[{0}] {1}" -f $Kind,$Text)
}

function Format-UnitCommand {
    param([string]$Executable,[string[]]$Arguments)
    $unit14Shown=foreach ($unit14Argument in $Arguments) {
        # Never echo secrets that might be passed as arguments.
        if ($unit14Argument -match '(?i)(token|password|secret|key)=') { ($unit14Argument -replace '=.*','=***') }
        elseif ($unit14Argument -match '\s') { '"' + $unit14Argument + '"' }
        else { $unit14Argument }
    }
    return (@($Executable)+@($unit14Shown)) -join ' '
}

function ConvertTo-UnitData {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) {
        $unit14Map=@{}
        foreach ($unit14Key in $Value.Keys) { $unit14Map[$unit14Key]=ConvertTo-UnitData -Value $Value[$unit14Key] }
        return $unit14Map
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $unit14Map=@{}
        foreach ($unit14Property in $Value.PSObject.Properties) {
            $unit14Map[$unit14Property.Name]=ConvertTo-UnitData -Value $unit14Property.Value
        }
        return $unit14Map
    }
    if ($Value -is [System.Array]) {
        $unit14Items=@()
        foreach ($unit14Item in $Value) { $unit14Items+=,(ConvertTo-UnitData -Value $unit14Item) }
        return ,$unit14Items
    }
    return $Value
}

function ConvertFrom-UnitJson {
    param([Parameter(Mandatory,ValueFromPipeline)][string]$Json)
    process {
        # Wrapping the value preserves empty and single-element JSON arrays in both shells.
        $unit14Wrapper=ConvertFrom-Json -InputObject ('{"value":' + $Json + '}')
        ConvertTo-UnitData -Value $unit14Wrapper.value
    }
}

function Set-UnitContent {
    param([Parameter(Mandatory,Position=0)][string]$Path,
        [Parameter(ValueFromPipeline)][AllowEmptyString()][string[]]$Value)
    begin { $unit14Lines=[System.Collections.Generic.List[string]]::new() }
    process { foreach ($unit14Line in $Value) { $unit14Lines.Add($unit14Line) } }
    end {
        # Native tools receive the same UTF-8 JSON without a BOM on every platform.
        [IO.File]::WriteAllLines($Path,$unit14Lines.ToArray(),[Text.UTF8Encoding]::new($false))
    }
}

function Get-UnitShellPath {
    if ($PSVersionTable.PSEdition -eq 'Desktop') { return Join-Path $PSHOME 'powershell.exe' }
    if ($unit14OnWindows) { return Join-Path $PSHOME 'pwsh.exe' }
    return Join-Path $PSHOME 'pwsh'
}

function Test-UnitTool {
    param([string]$Executable,[string[]]$Arguments)
    Write-UnitTrace -Kind 'CHECK COMMAND' -Text (Format-UnitCommand -Executable $Executable -Arguments $Arguments)
    $unit14PreviousPreference=$ErrorActionPreference
    try {
        # An unavailable engine is an expected check, including native stderr in PowerShell 5.1.
        $ErrorActionPreference='Continue'
        & $Executable @Arguments 2>$null | Out-Null
        $unit14Ok=$LASTEXITCODE -eq 0
        Write-UnitTrace -Kind 'CHECK' -Text ('{0} | {1} exit code {2}' -f $(if ($unit14Ok) {'PASS'} else {'FAIL'}),$Executable,$LASTEXITCODE)
        return $unit14Ok
    } finally { $ErrorActionPreference=$unit14PreviousPreference }
}

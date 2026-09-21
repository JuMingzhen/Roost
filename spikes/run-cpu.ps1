[CmdletBinding()]
param(
    [ValidateRange(1, 3600)]
    [int]$VisibleSeconds = 600,
    [ValidateRange(1, 600)]
    [int]$HiddenSeconds = 30,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'out\cpu.json')
)

$ErrorActionPreference = 'Stop'
$exe = & (Join-Path $PSScriptRoot 'build.ps1') | Select-Object -Last 1
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
& $exe cpu $OutputPath $VisibleSeconds $HiddenSeconds
$exitCode = $LASTEXITCODE
Get-Content -Raw -LiteralPath $OutputPath
exit $exitCode

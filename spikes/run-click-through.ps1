[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'out\click-through.json')
)

$ErrorActionPreference = 'Stop'
$exe = & (Join-Path $PSScriptRoot 'build.ps1') | Select-Object -Last 1
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
& $exe click-through $OutputPath
$exitCode = $LASTEXITCODE
Get-Content -Raw -LiteralPath $OutputPath
exit $exitCode

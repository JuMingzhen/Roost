[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'out\pixel-clarity.json'),
    [string]$ScreenshotDirectory = (Join-Path $PSScriptRoot 'out\pixel-screenshots')
)

$ErrorActionPreference = 'Stop'
$exe = & (Join-Path $PSScriptRoot 'build.ps1') | Select-Object -Last 1
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
New-Item -ItemType Directory -Force -Path $ScreenshotDirectory | Out-Null
& $exe pixel-clarity $OutputPath $ScreenshotDirectory
$exitCode = $LASTEXITCODE
Get-Content -Raw -LiteralPath $OutputPath
exit $exitCode

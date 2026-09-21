[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(100, 125, 150, 200)]
    [int]$ExpectedScalePercent
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root ("artifacts\m1\dpi-{0}" -f $ExpectedScalePercent)
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host

$systemPath = Join-Path $artifactRoot 'system.json'
$pixelPath = Join-Path $artifactRoot 'pixel.json'
$screenshots = Join-Path $artifactRoot 'pixel-screenshots'
& (Join-Path $root 'dist\Roost.Verify.exe') system $systemPath
if ($LASTEXITCODE -ne 0) { throw '真实 DPI 下的点击穿透验证失败。' }
& (Join-Path $root 'dist\Roost.Verify.exe') pixel $pixelPath $screenshots
if ($LASTEXITCODE -ne 0) { throw '真实 DPI 下的像素验证失败。' }

$system = Get-Content -Raw -LiteralPath $systemPath | ConvertFrom-Json
$pixel = Get-Content -Raw -LiteralPath $pixelPath | ConvertFrom-Json
if ($system.actualScalePercent -ne $ExpectedScalePercent -or $pixel.actualScalePercent -ne $ExpectedScalePercent) {
    throw "当前验证进程检测到的缩放不是 $ExpectedScalePercent%：system=$($system.actualScalePercent)%，pixel=$($pixel.actualScalePercent)%。请确认 Windows 缩放已经生效后重跑。"
}
if (-not $system.overallPass -or -not $pixel.overallPass -or -not $pixel.actualHardwarePass) {
    throw "真实 $ExpectedScalePercent% 缩放验证未通过。"
}

Write-Output "PASS real Windows scale $ExpectedScalePercent%"
Write-Output "Evidence: $artifactRoot"
Get-Content -Raw -LiteralPath $systemPath
Get-Content -Raw -LiteralPath $pixelPath


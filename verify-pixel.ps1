[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root 'artifacts\m1'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'dist\Roost.Verify.exe') pixel (Join-Path $artifactRoot 'pixel.json') (Join-Path $artifactRoot 'pixel-screenshots')
if ($LASTEXITCODE -ne 0) { throw '像素清晰度验证失败。' }
Get-Content -Raw (Join-Path $artifactRoot 'pixel.json')

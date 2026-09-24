[CmdletBinding()]
param(
    [string]$ArtifactRoot = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $root 'artifacts\layout' }
New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'dist\Roost.Verify.exe') layout (Join-Path $ArtifactRoot 'layout.json') (Join-Path $ArtifactRoot 'layout-screenshots')
if ($LASTEXITCODE -ne 0) { throw '窗口排版验证失败：有文字放不下、控件重叠或超出容器。' }
Get-Content -Raw (Join-Path $ArtifactRoot 'layout.json')

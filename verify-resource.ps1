[CmdletBinding()]
param(
    [int]$VisibleSeconds = 600,
    [int]$HiddenSeconds = 30
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root 'artifacts\m1'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'dist\Roost.Verify.exe') cpu (Join-Path $artifactRoot 'resource.json') $VisibleSeconds $HiddenSeconds
if ($LASTEXITCODE -ne 0) { throw '资源占用验证失败。正式验证要求 VisibleSeconds 至少为 600。' }
Get-Content -Raw (Join-Path $artifactRoot 'resource.json')

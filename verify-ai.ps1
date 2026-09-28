[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root 'artifacts\m2'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'dist\Roost.Verify.exe') ai (Join-Path $artifactRoot 'ai-flow.json')
if ($LASTEXITCODE -ne 0) { throw 'AI 改计划流程验证失败。' }
Get-Content -Raw (Join-Path $artifactRoot 'ai-flow.json')

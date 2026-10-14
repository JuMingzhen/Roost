[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root 'artifacts\m3'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'dist\Roost.Verify.exe') reminder (Join-Path $artifactRoot 'reminder.json') (Join-Path $artifactRoot 'reminder-screenshots')
if ($LASTEXITCODE -ne 0) { throw '提醒流程验证失败。' }
Get-Content -Raw -Encoding UTF8 (Join-Path $artifactRoot 'reminder.json')

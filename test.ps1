[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'bin\Roost.Tests.exe')
if ($LASTEXITCODE -ne 0) {
    throw "测试失败：$LASTEXITCODE"
}


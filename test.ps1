[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'verify-powershell.ps1') | Out-Host
& (Join-Path $root 'build.ps1') | Out-Host
& (Join-Path $root 'bin\Roost.Tests.exe')
if ($LASTEXITCODE -ne 0) {
    throw "测试失败：$LASTEXITCODE"
}
& (Join-Path $root 'bin\Roost.Eval.exe') --self-test (Join-Path $root 'tests\ai-eval\cases.json')
if ($LASTEXITCODE -ne 0) {
    throw "AI 评测集自检失败：$LASTEXITCODE"
}

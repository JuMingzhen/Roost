[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('deepseek', 'qwen', 'zhipu', 'doubao', 'custom')]
    [string]$Preset,
    [string]$Model = '',
    [string]$BaseUrl = '',
    [switch]$SaveKey
)

# 用真实模型跑 AI 评测集（PRD 8.6）。key 只在本进程和子进程的环境变量里停留；
# 加 -SaveKey 时存进 Windows 凭据管理器（目标 Roost/Eval/<预设>），以后不再询问。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1') | Out-Host
$eval = Join-Path $root 'bin\Roost.Eval.exe'

& $eval has-key $Preset
$hasSavedKey = $LASTEXITCODE -eq 0
$typedKey = $false
if (-not $hasSavedKey) {
    $secure = Read-Host -AsSecureString "请输入 $Preset 的 API Key（输入内容不会显示）"
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $env:ROOST_EVAL_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $typedKey = $true
}

try {
    if ($typedKey -and $SaveKey) {
        & $eval save-key $Preset
        if ($LASTEXITCODE -ne 0) { throw '保存 key 失败。' }
    }
    $label = if ($Model) { $Model } else { 'default' }
    $safeLabel = ($label -replace '[^A-Za-z0-9._-]', '_')
    $report = Join-Path $root ("artifacts\m2\eval-{0}-{1}.json" -f $Preset, $safeLabel)
    & $eval run (Join-Path $root 'tests\ai-eval\cases.json') $Preset $report $BaseUrl $Model
    $code = $LASTEXITCODE
    Write-Output "Report: $report"
    if ($code -eq 1) { throw "$Preset 未通过评测集（致命错误必须为 0，正确率 ≥ 90%）。" }
    if ($code -ne 0) { throw "评测没有完成：$code" }
}
finally {
    Remove-Item Env:\ROOST_EVAL_KEY -ErrorAction SilentlyContinue
}

[CmdletBinding()]
param(
    [switch]$Quick
)

$ErrorActionPreference = 'Stop'

$failed = @()

& (Join-Path $PSScriptRoot 'run-click-through.ps1')
if ($LASTEXITCODE -ne 0) { $failed += '点击穿透' }

& (Join-Path $PSScriptRoot 'run-pixel-clarity.ps1')
if ($LASTEXITCODE -ne 0) { $failed += '像素清晰' }

$visibleSeconds = if ($Quick) { 20 } else { 600 }
$hiddenSeconds = if ($Quick) { 5 } else { 30 }
& (Join-Path $PSScriptRoot 'run-cpu.ps1') -VisibleSeconds $visibleSeconds -HiddenSeconds $hiddenSeconds
if ($LASTEXITCODE -ne 0) { $failed += '待机 CPU' }

if ($failed.Count -gt 0) {
    Write-Output ("未完全通过：" + ($failed -join '、') + '。请查看 JSON 的 status 和 hardwareCoverage。')
    exit 1
}

Write-Output '三个 spike 均通过。'

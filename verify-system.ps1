[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $root 'artifacts\m1'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
& (Join-Path $root 'build.ps1') | Out-Host

& (Join-Path $root 'dist\Roost.Verify.exe') system (Join-Path $artifactRoot 'system.json')
if ($LASTEXITCODE -ne 0) { throw '桌面系统集成验证失败。' }

$testRoot = Join-Path $env:TEMP ('roost-single-instance-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$readyFile = Join-Path $testRoot 'ready'
$showFile = Join-Path $testRoot 'shown'
$env:ROOST_DATA_DIR = $testRoot
$env:ROOST_TEST_READY_FILE = $readyFile
$env:ROOST_TEST_SHOW_FILE = $showFile
$env:ROOST_SKIP_FIRST_RUN = '1'
$env:ROOST_SKIP_HOTKEY_WARNING = '1'
$first = $null
try {
    $first = Start-Process -FilePath (Join-Path $root 'dist\Roost.exe') -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    while (-not (Test-Path -LiteralPath $readyFile) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 50
    }
    if (-not (Test-Path -LiteralPath $readyFile)) { throw '首个 Roost 实例未就绪。' }

    $second = Start-Process -FilePath (Join-Path $root 'dist\Roost.exe') -PassThru -Wait
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while (-not (Test-Path -LiteralPath $showFile) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 50
    }
    $first.Refresh()
    $pass = $second.ExitCode -eq 0 -and -not $first.HasExited -and (Test-Path -LiteralPath $showFile)
    $result = [ordered]@{
        test = 'single-instance'
        timestampUtc = [DateTime]::UtcNow.ToString('o')
        secondExitCode = $second.ExitCode
        firstStillRunning = -not $first.HasExited
        existingInstanceReceivedShowMessage = Test-Path -LiteralPath $showFile
        overallPass = $pass
        status = if ($pass) { 'PASS' } else { 'FAIL' }
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'single-instance.json') -Encoding UTF8
    if (-not $pass) { throw '单实例验证失败。' }
}
finally {
    if ($first -and -not $first.HasExited) { $first.Kill(); $first.WaitForExit() }
    Remove-Item Env:\ROOST_DATA_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:\ROOST_TEST_READY_FILE -ErrorAction SilentlyContinue
    Remove-Item Env:\ROOST_TEST_SHOW_FILE -ErrorAction SilentlyContinue
    Remove-Item Env:\ROOST_SKIP_FIRST_RUN -ErrorAction SilentlyContinue
    Remove-Item Env:\ROOST_SKIP_HOTKEY_WARNING -ErrorAction SilentlyContinue
}

Get-Content -Raw (Join-Path $artifactRoot 'system.json')
Get-Content -Raw (Join-Path $artifactRoot 'single-instance.json')


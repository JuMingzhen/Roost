[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$spikeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$binDir = Join-Path $spikeRoot 'bin'
$source = Join-Path $spikeRoot 'src\RoostSpike.cs'
$output = Join-Path $binDir 'RoostSpike.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "未找到系统 C# 编译器：$compiler"
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null

$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll'
)

$arguments = @(
    '/nologo',
    '/target:exe',
    '/optimize+',
    '/debug-',
    '/platform:anycpu',
    "/out:$output"
) + ($references | ForEach-Object { "/reference:$_" }) + @($source)

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Spike 编译失败，csc 退出码：$LASTEXITCODE"
}

Write-Output $output

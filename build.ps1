[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$bin = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "未找到系统 C# 编译器：$compiler"
}

New-Item -ItemType Directory -Force -Path $bin, $dist | Out-Null

$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\Roost.Core') -Filter '*.cs' | ForEach-Object FullName)
$coreArguments = @(
    '/nologo', '/target:library', '/optimize+', '/debug-', '/platform:anycpu',
    "/out:$(Join-Path $bin 'Roost.Core.dll')",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll'
) + $coreSources
& $compiler $coreArguments
if ($LASTEXITCODE -ne 0) { throw "Roost.Core 编译失败：$LASTEXITCODE" }

$testSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests\Roost.Tests') -Filter '*.cs' | ForEach-Object FullName)
$testArguments = @(
    '/nologo', '/target:exe', '/optimize+', '/debug-', '/platform:anycpu',
    "/out:$(Join-Path $bin 'Roost.Tests.exe')",
    "/reference:$(Join-Path $bin 'Roost.Core.dll')",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll'
) + $testSources
& $compiler $testArguments
if ($LASTEXITCODE -ne 0) { throw "Roost.Tests 编译失败：$LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $bin 'Roost.Core.dll') -Destination $dist -Force
Write-Output "Build succeeded: $bin"

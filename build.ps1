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

$appSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\Roost.App') -Filter '*.cs' | ForEach-Object FullName)
$appArguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/debug-', '/platform:anycpu',
    "/out:$(Join-Path $dist 'Roost.exe')",
    "/reference:$(Join-Path $bin 'Roost.Core.dll')",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll'
) + $appSources
& $compiler $appArguments
if ($LASTEXITCODE -ne 0) { throw "Roost.App 编译失败：$LASTEXITCODE" }

$testSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests\Roost.Tests') -Filter '*.cs' | ForEach-Object FullName)
$testArguments = @(
    '/nologo', '/target:exe', '/optimize+', '/debug-', '/platform:anycpu',
    "/out:$(Join-Path $bin 'Roost.Tests.exe')",
    "/reference:$(Join-Path $bin 'Roost.Core.dll')",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll'
) + $testSources
& $compiler $testArguments
if ($LASTEXITCODE -ne 0) { throw "Roost.Tests 编译失败：$LASTEXITCODE" }

$verifySources = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests\Roost.Verify') -Filter '*.cs' | ForEach-Object FullName)
$verifyArguments = @(
    '/nologo', '/target:exe', '/optimize+', '/debug-', '/platform:anycpu',
    "/out:$(Join-Path $dist 'Roost.Verify.exe')",
    "/reference:$(Join-Path $dist 'Roost.exe')",
    "/reference:$(Join-Path $bin 'Roost.Core.dll')",
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Web.Extensions.dll'
) + $verifySources
& $compiler $verifyArguments
if ($LASTEXITCODE -ne 0) { throw "Roost.Verify 编译失败：$LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $bin 'Roost.Core.dll') -Destination $dist -Force
$assetDestination = Join-Path $dist 'assets'
if (Test-Path -LiteralPath $assetDestination) {
    $resolvedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $resolvedAssets = [System.IO.Path]::GetFullPath($assetDestination)
    if (-not $resolvedAssets.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝删除工作区以外的构建目录：$resolvedAssets"
    }
    Remove-Item -LiteralPath $assetDestination -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination $assetDestination -Recurse -Force
Write-Output "Build succeeded: $bin"

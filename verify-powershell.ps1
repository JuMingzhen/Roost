[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$relativeFiles = @(& git -C $root ls-files '*.ps1')
if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
if ($relativeFiles -notcontains 'verify-powershell.ps1') {
    $relativeFiles += 'verify-powershell.ps1'
}

$failures = @()
foreach ($relativeFile in $relativeFiles) {
    $path = Join-Path $root $relativeFile
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    if (-not $hasUtf8Bom) {
        $failures += "$relativeFile does not have a UTF-8 BOM."
    }

    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors) | Out-Null
    foreach ($parseError in $parseErrors) {
        $failures += "${relativeFile}:$($parseError.Extent.StartLineNumber): $($parseError.Message)"
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw 'PowerShell compatibility check failed.'
}

Write-Output "PASS PowerShell syntax and UTF-8 BOM ($($relativeFiles.Count) scripts)"

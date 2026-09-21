[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'build.ps1') | Out-Host
Start-Process -FilePath (Join-Path $root 'dist\Roost.exe')


[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'test.ps1')
& (Join-Path $root 'verify-system.ps1')
& (Join-Path $root 'verify-pixel.ps1')
& (Join-Path $root 'verify-resource.ps1')


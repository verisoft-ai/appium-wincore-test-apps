#Requires -Version 5.1
<#
.SYNOPSIS
  Downloads the pinned tn5250j release jar into tn5250j\bin and verifies its SHA-256.
  Skips the download when the jar is already there with the right hash.

.NOTES
  tn5250j is GPL-2.0, so the jar is fetched from its GitHub release rather than committed.
#>
$ErrorActionPreference = 'Stop'

$version = '0.8.0-beta2'
# Upstream's asset name really is misspelled ("depedencies").
$jarName = "tn5250j-$version-no-depedencies.jar"
$url = "https://github.com/tn5250j/tn5250j/releases/download/$version/$jarName"
$sha256 = 'E972D6F2F437FFBDBB42F556D8B9234CD848C063017F0072AD7EA5CEA9DB8451'

$bin = Join-Path $PSScriptRoot 'bin'
$jar = Join-Path $bin $jarName

if ((Test-Path $jar) -and (Get-FileHash $jar -Algorithm SHA256).Hash -eq $sha256) {
    Write-Host "tn5250j $version already present: $jar"
    return
}

New-Item -ItemType Directory -Force $bin | Out-Null
$tmp = "$jar.download"
Write-Host "Downloading $url"
# PS 5.1's progress bar slows Invoke-WebRequest down by an order of magnitude.
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing

$actual = (Get-FileHash $tmp -Algorithm SHA256).Hash
if ($actual -ne $sha256) {
    Remove-Item $tmp -Force
    throw "SHA-256 mismatch for $jarName`: expected $sha256, got $actual"
}
Move-Item $tmp $jar -Force
Write-Host "tn5250j $version ready: $jar"

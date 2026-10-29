#Requires -Version 5.1
<#
.SYNOPSIS
  Launches tn5250j on a 5250 session from config\sessions (default PUB400), with its
  settings kept inside this fixture instead of %USERPROFILE%\.tn5250j.

.PARAMETER Session
  Session name from config\sessions.

.PARAMETER Wait
  Block until tn5250j exits instead of returning once it has started.

.PARAMETER ReaderAgent
  Path to the appium-pcomm-plugin reader agent jar (native\tn5250j-reader\build\tn5250j-reader.jar).
  Loaded with -javaagent so the plugin's tn5250j backend can read the screen; it listens on
  loopback only and writes its port to %TEMP%\pcomm-tn5250j-<pid>.port.

.NOTES
  tn5250j rewrites its settings directory as it runs (window position, last session), so
  config\ is copied fresh into bin\settings on every launch: the committed files stay clean
  and every run starts from the same state.

  -nc skips tn5250j's single-instance bootstrapper, which would otherwise hand the session
  to an already-running tn5250j and exit - each launch gets its own process.

  Run fetch.ps1 first (npm run build:tn5250j-test-app).
#>
param(
    [string]$Session = 'PUB400',
    [switch]$Wait,
    [string]$ReaderAgent
)

$ErrorActionPreference = 'Stop'

$jar = Get-ChildItem (Join-Path $PSScriptRoot 'bin') -Filter 'tn5250j-*.jar' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $jar) {
    throw "tn5250j jar not found in $PSScriptRoot\bin - run fetch.ps1 first"
}

$settings = Join-Path $PSScriptRoot 'bin\settings'
New-Item -ItemType Directory -Force $settings | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'config\*') $settings -Force

$java = if ($env:JAVA_HOME) { Join-Path $env:JAVA_HOME 'bin\javaw.exe' } else { 'javaw.exe' }
$javaArgs = @()
if ($ReaderAgent) {
    $agent = (Resolve-Path $ReaderAgent).Path
    $javaArgs += "-javaagent:`"$agent`""
}
$javaArgs += @("-Demulator.settingsDirectory=`"$settings`"", '-jar', "`"$($jar.FullName)`"",
    '-s', $Session, '-nc')

Write-Host "$java $($javaArgs -join ' ')"
$proc = Start-Process -FilePath $java -ArgumentList $javaArgs -PassThru -Wait:$Wait
Write-Host "tn5250j started (pid $($proc.Id))"

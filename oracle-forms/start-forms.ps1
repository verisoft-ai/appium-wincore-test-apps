#Requires -Version 5.1
<#
.SYNOPSIS
  Makes sure the Forms domain is serving: starts AdminServer (7001) and WLS_FORMS (9001)
  if they aren't answering, then waits for the Forms servlet.

.PARAMETER Register
  Instead of starting now, registers a Scheduled Task that runs this script at system
  start-up (as SYSTEM), so the runner finds Forms already up. Run once from an elevated
  PowerShell.

.NOTES
  Both servers read their credentials from servers\<name>\security\boot.properties
  (README.md, "Environment and server credentials") - this script never handles a password.
#>
param(
    [string]$DomainHome = $(if ($env:DOMAIN_HOME) { $env:DOMAIN_HOME } else { 'C:\Oracle\Middleware\Oracle_Home\user_projects\domains\base_domain' }),
    [int]$TimeoutSeconds = 600,
    [switch]$Register
)

$ErrorActionPreference = 'Stop'

if ($Register) {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -DomainHome `"$DomainHome`""
    $trigger = New-ScheduledTaskTrigger -AtStartup
    Register-ScheduledTask -TaskName 'Wincore Oracle Forms' -Action $action -Trigger $trigger `
        -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Write-Host "Registered scheduled task 'Wincore Oracle Forms' (runs at start-up)."
    return
}

function Test-Url([string]$url) {
    try { return (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode -lt 500 } catch { return $false }
}

function Wait-Url([string]$url, [string]$what) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Url $url) { Write-Host "$what is up."; return }
        Start-Sleep -Seconds 5
    }
    throw "$what did not come up within ${TimeoutSeconds}s ($url)"
}

$bin = Join-Path $DomainHome 'bin'
$logs = Join-Path $DomainHome 'servers\wincore-start-logs'
New-Item -ItemType Directory -Force $logs | Out-Null

if (-not (Test-Url 'http://localhost:7001/console')) {
    Write-Host 'Starting AdminServer...'
    Start-Process -FilePath (Join-Path $bin 'startWebLogic.cmd') -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logs 'AdminServer.out') -RedirectStandardError (Join-Path $logs 'AdminServer.err')
    Wait-Url 'http://localhost:7001/console' 'AdminServer'
}

if (-not (Test-Url 'http://localhost:9001/forms/frmservlet')) {
    Write-Host 'Starting WLS_FORMS...'
    Start-Process -FilePath (Join-Path $bin 'startManagedWebLogic.cmd') -ArgumentList 'WLS_FORMS', 't3://localhost:7001' -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logs 'WLS_FORMS.out') -RedirectStandardError (Join-Path $logs 'WLS_FORMS.err')
    Wait-Url 'http://localhost:9001/forms/frmservlet' 'WLS_FORMS'
}

Write-Host 'Oracle Forms is serving on http://localhost:9001/forms/frmservlet'

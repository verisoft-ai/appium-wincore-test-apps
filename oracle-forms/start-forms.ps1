#Requires -Version 5.1
<#
.SYNOPSIS
  Makes sure the Forms domain is serving: starts AdminServer (7001) and WLS_FORMS (9001)
  if they aren't answering, then waits for the Forms servlet.

.PARAMETER Register
  Instead of starting now, registers a Scheduled Task that runs this script when the
  current user logs on, not elevated, so the runner finds Forms already up.

.NOTES
  Both servers read their credentials from servers\<name>\security\boot.properties
  (README.md, "Environment and server credentials") - this script never handles a password.

  Always start the servers as the runner's user, not elevated: the AdminServer's credential
  store creates bootstrap\cwallet.sso.lck with owner-only permissions, and a lock left by an
  elevated or SYSTEM start makes every later non-elevated start fail with JPS-01050.
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
    $user = "${env:USERDOMAIN}\${env:USERNAME}"
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
    $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName 'Wincore Oracle Forms' -Action $action -Trigger $trigger `
        -Principal $principal -Force | Out-Null
    Write-Host "Registered scheduled task 'Wincore Oracle Forms' (runs at $user's logon, not elevated)."
    return
}

# HTTP status of $url, or 0 when nothing answers. Invoke-WebRequest throws on 4xx/5xx, so
# the status is read from the exception.
function Get-Status([string]$url) {
    try { return [int](Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

# Up = answering and not 503. WebLogic's /weblogic/ready returns 503 until the server is
# RUNNING; 14.1.2 has no /console to probe.
function Test-Up([string]$url) {
    $s = Get-Status $url
    return $s -ne 0 -and $s -ne 503
}

function Wait-Up([string]$url, [string]$what, [string]$log) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Up $url) { Write-Host "$what is up."; return }
        Start-Sleep -Seconds 5
    }
    Write-Host "--- last 40 lines of $log ---"
    Get-Content $log -Tail 40 -ErrorAction SilentlyContinue
    throw "$what did not come up within ${TimeoutSeconds}s ($url)"
}

$bin = Join-Path $DomainHome 'bin'
$logs = Join-Path $DomainHome 'servers\wincore-start-logs'
New-Item -ItemType Directory -Force $logs | Out-Null

$adminReady = 'http://localhost:7001/weblogic/ready'
if (-not (Test-Up $adminReady)) {
    Write-Host 'Starting AdminServer...'
    $out = Join-Path $logs 'AdminServer.out'
    Start-Process -FilePath (Join-Path $bin 'startWebLogic.cmd') -WorkingDirectory $DomainHome -WindowStyle Hidden `
        -RedirectStandardOutput $out -RedirectStandardError (Join-Path $logs 'AdminServer.err')
    Wait-Up $adminReady 'AdminServer' $out
}

# The managed server's own readiness endpoint, so a half-deployed formsapp (404) isn't "up".
$formsUrl = 'http://localhost:9001/weblogic/ready'
if (-not (Test-Up $formsUrl)) {
    Write-Host 'Starting WLS_FORMS...'
    $out = Join-Path $logs 'WLS_FORMS.out'
    Start-Process -FilePath (Join-Path $bin 'startManagedWebLogic.cmd') -ArgumentList 'WLS_FORMS', 't3://localhost:7001' `
        -WorkingDirectory $DomainHome -WindowStyle Hidden `
        -RedirectStandardOutput $out -RedirectStandardError (Join-Path $logs 'WLS_FORMS.err')
    Wait-Up $formsUrl 'WLS_FORMS' $out
}

Write-Host 'Oracle Forms is serving on http://localhost:9001/forms/frmservlet'

#Requires -Version 5.1
<#
.SYNOPSIS
  Builds and deploys the Oracle Forms fixture: generates wincore_fixture.fmb with JDAPI,
  compiles it to .fmx, builds + signs the VTextFieldEng PJC jar, and registers a [wincore]
  section in formsweb.cfg so the Forms Standalone App Launcher can open it with
  ?config=wincore.

.NOTES
  Idempotent - safe to re-run (the CI workflow runs it on every job). Needs an installed
  Oracle Forms (see README.md). Paths default to the README's install layout; override
  with parameters or the ORACLE_HOME / DOMAIN_HOME environment variables.
#>
param(
    [string]$OracleHome = $(if ($env:ORACLE_HOME) { $env:ORACLE_HOME } else { 'C:\Oracle\Middleware\Oracle_Home' }),
    [string]$DomainHome = $(if ($env:DOMAIN_HOME) { $env:DOMAIN_HOME } else { 'C:\Oracle\Middleware\Oracle_Home\user_projects\domains\base_domain' }),
    [string]$ConfigName = 'wincore'
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$out = Join-Path $here 'build'
New-Item -ItemType Directory -Force $out | Out-Null

function Require-Path([string]$path, [string]$what) {
    if (-not (Test-Path $path)) { throw "$what not found at $path - is Oracle Forms installed (README.md, 'FMW Infrastructure and Forms')?" }
    return (Resolve-Path $path).Path
}

$jdapiJar = Require-Path (Join-Path $OracleHome 'jlib\frmjdapi.jar') 'JDAPI jar (frmjdapi.jar)'
$frmallJar = Require-Path (Join-Path $OracleHome 'forms\java\frmall.jar') 'Forms client jar (frmall.jar)'

# A 14.1.2 domain on Windows has no per-instance frmcmp_batch script (the instance's tools
# folder only holds jvm), so call the compiler in ORACLE_HOME\bin directly.
$frmcmp = Require-Path (Join-Path $OracleHome 'bin\frmcmp.exe') 'Forms compiler (frmcmp.exe)'

$formsweb = Get-ChildItem (Join-Path $DomainHome 'config\fmwconfig\servers\WLS_FORMS\applications') -Recurse -Filter 'formsweb.cfg' -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $formsweb) { throw "formsweb.cfg not found under $DomainHome - has the Forms domain been configured (README.md, 'Schemas and domain')?" }

Write-Host "ORACLE_HOME:  $OracleHome"
Write-Host "DOMAIN_HOME:  $DomainHome"
Write-Host "frmcmp:       $frmcmp"
Write-Host "formsweb.cfg: $formsweb"

# -- 1. .fmb via JDAPI ------------------------------------------------------------
# JDAPI is a thin Java layer over the Forms native libraries, so ORACLE_HOME\bin must be on PATH.
$env:PATH = "$(Join-Path $OracleHome 'bin');$env:PATH"
$genOut = Join-Path $out 'generator'
New-Item -ItemType Directory -Force $genOut | Out-Null
& javac -cp $jdapiJar -d $genOut (Join-Path $here 'generator\GenerateFixture.java')
if ($LASTEXITCODE -ne 0) { throw 'javac (generator) failed' }
$fmb = Join-Path $out 'wincore_fixture.fmb'
& java -cp "$jdapiJar;$genOut" GenerateFixture $fmb
if ($LASTEXITCODE -ne 0) { throw 'GenerateFixture failed' }

# -- 2. .fmx ----------------------------------------------------------------------
# logon=no: the form has no database items, so the compiler needs no connection.
# batch=yes: no dialogs. frmcmp.exe is a GUI-subsystem program, which PowerShell's & doesn't
# wait for, so start it explicitly and wait on its exit code.
$fmx = Join-Path $out 'wincore_fixture.fmx'
Remove-Item $fmx -ErrorAction SilentlyContinue
$env:ORACLE_HOME = $OracleHome
$cmp = Start-Process -FilePath $frmcmp -Wait -PassThru -NoNewWindow -WorkingDirectory $out `
    -ArgumentList "module=$fmb", 'module_type=form', 'logon=no', 'batch=yes', 'compile_all=yes', "output_file=$fmx"
if ($cmp.ExitCode -ne 0 -or -not (Test-Path $fmx)) {
    Get-ChildItem $out -Filter '*.err' | ForEach-Object { Write-Host "--- $($_.Name) ---"; Get-Content $_.FullName }
    throw "frmcmp.exe failed (exit $($cmp.ExitCode); see .err output above)"
}

# -- 3. PJC jar -------------------------------------------------------------------
$pjcOut = Join-Path $out 'pjc'
New-Item -ItemType Directory -Force $pjcOut | Out-Null
& javac --release 8 -cp $frmallJar -d $pjcOut (Join-Path $here 'pjc\VTextFieldEng.java')
if ($LASTEXITCODE -ne 0) { throw 'javac (PJC) failed' }
$pjcJar = Join-Path $out 'wincore-pjc.jar'
& jar cf $pjcJar -C $pjcOut .
if ($LASTEXITCODE -ne 0) { throw 'jar (PJC) failed' }

# Forms refuses unsigned custom jars. A throwaway self-signed key is enough for a local fixture.
$keystore = Join-Path $out 'wincore-fixture.jks'
$storePass = 'wincore-fixture'
if (-not (Test-Path $keystore)) {
    & keytool -genkeypair -alias wincore -keyalg RSA -keysize 2048 -validity 3650 `
        -dname 'CN=wincore-fixture' -keystore $keystore -storepass $storePass -keypass $storePass
    if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }
}
& jarsigner -keystore $keystore -storepass $storePass $pjcJar wincore
if ($LASTEXITCODE -ne 0) { throw 'jarsigner failed' }
Copy-Item $pjcJar (Join-Path $OracleHome 'forms\java\wincore-pjc.jar') -Force

# -- 4. Deploy the .fmx -----------------------------------------------------------
# The Forms runtime only opens modules found on FORMS_PATH / FORMS_MODULE_PATH; a form
# outside them (like this build folder) fails with FRM-40010. ORACLE_HOME\forms is on the
# default FORMS_PATH, so deploy there and reference the form by name.
$deployedFmx = Join-Path $OracleHome 'forms\wincore_fixture.fmx'
Copy-Item $fmx $deployedFmx -Force
$defaultEnv = Join-Path (Split-Path $formsweb) 'default.env'
Select-String $defaultEnv -Pattern '^(FORMS_PATH|FORMS_MODULE_PATH)=' -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host "default.env: $($_.Line)" }
Write-Host "Deployed $deployedFmx"

# -- 5. formsweb.cfg [wincore] section --------------------------------------------
$section = @"
[$ConfigName]
form=wincore_fixture.fmx
archive=frmall.jar,wincore-pjc.jar
lookAndFeel=Generic
"@
$cfg = Get-Content $formsweb -Raw
$pattern = "(?ms)^\[$([regex]::Escape($ConfigName))\]\r?\n.*?(?=^\[|\z)"
if ($cfg -match $pattern) {
    $cfg = [regex]::Replace($cfg, $pattern, "$section`r`n`r`n")
} else {
    if (-not $cfg.EndsWith("`n")) { $cfg += "`r`n" }
    $cfg += "`r`n$section`r`n"
}
Set-Content -Path $formsweb -Value $cfg -NoNewline -Encoding ASCII

Write-Host ''
Write-Host "Fixture ready: http://localhost:9001/forms/frmservlet?config=$ConfigName"

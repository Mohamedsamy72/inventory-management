@echo off
REM ===========================================================================
REM  run-local.bat - Master local launcher.   docs/19 section 2.1
REM
REM  CRITICAL DATABASE SAFETY INVARIANT (docs/19, docs/25):
REM  This script NEVER drops a database, NEVER deletes a data directory, and
REM  NEVER performs a destructive schema reset. It applies EF Core migrations
REM  forward only. StartupScriptRules enforces this in the test suite.
REM ===========================================================================

setlocal EnableDelayedExpansion
set "ROOT=%~dp0"
set "API_URL=http://localhost:5165"
set "WEB_URL=http://localhost:3000"

echo ============================================================
echo  Restaurant Inventory System - local launcher
echo ============================================================
echo.

REM ---- 1. Prerequisites --------------------------------------------------
echo [1/8] Checking prerequisites...

where dotnet >nul 2>&1
if errorlevel 1 (
  echo   FAILED: 'dotnet' not found on PATH. Install the .NET 10 SDK.
  goto :fail
)
for /f "delims=" %%v in ('dotnet --version') do set "DOTNET_VERSION=%%v"
echo   dotnet          : !DOTNET_VERSION!

where node >nul 2>&1
if errorlevel 1 (
  echo   FAILED: 'node' not found on PATH. Node.js 20.9+ is required.
  echo          Node is installed at D:\Nodejs on this machine - add it to PATH.
  goto :fail
)
for /f "delims=" %%v in ('node --version') do set "NODE_VERSION=%%v"
echo   node            : !NODE_VERSION!

REM ---- 2. PostgreSQL ------------------------------------------------------
echo [2/8] Checking PostgreSQL on port 5432...

netstat -an | findstr /C:"127.0.0.1:5432" /C:"0.0.0.0:5432" >nul 2>&1
if errorlevel 1 (
  echo.
  echo   ============================================================
  echo    FAILED: PostgreSQL is NOT listening on port 5432.
  echo   ============================================================
  echo    PostgreSQL 16+ is a hard prerequisite. Install it, then
  echo    re-run this script.
  echo.
  echo    This script will NOT substitute SQL Server, MySQL or any
  echo    other engine. The schema depends on PostgreSQL-specific
  echo    behaviour - xmin concurrency, ON CONFLICT ... RETURNING,
  echo    jsonb, range partitioning, pg_trgm, ICU collation.
  echo    See docs/decision-log.md ADR-027.
  echo   ============================================================
  goto :fail
)
echo   PostgreSQL      : listening on 5432

REM ---- 3. Migrations ------------------------------------------------------
echo [3/8] Applying EF Core migrations (forward only)...
echo   (no migration exists until Phase 2 - this is a no-op today)

REM ---- 4. Backend ---------------------------------------------------------
echo [4/8] Starting the API...
start "Inventory.Api" /min cmd /c "dotnet run --project ""%ROOT%src\Inventory.Api"" > ""%ROOT%logs-api.txt"" 2>&1"

REM ---- 5. API readiness ---------------------------------------------------
echo [5/8] Waiting for %API_URL%/health ...
set /a ATTEMPTS=0
:waitapi
set /a ATTEMPTS+=1
if !ATTEMPTS! GTR 60 (
  echo   FAILED: API did not become healthy. See logs-api.txt
  goto :fail
)
timeout /t 2 /nobreak >nul
curl -s -o nul -w "" %API_URL%/health >nul 2>&1
if errorlevel 1 goto :waitapi
echo   API             : healthy

REM ---- 6. Frontend --------------------------------------------------------
echo [6/8] Starting the frontend...
start "Inventory.Web" /min cmd /c "npm run dev --prefix ""%ROOT%frontend"" > ""%ROOT%logs-web.txt"" 2>&1"

REM ---- 7. Frontend readiness ---------------------------------------------
echo [7/8] Waiting for %WEB_URL% ...
set /a ATTEMPTS=0
:waitweb
set /a ATTEMPTS+=1
if !ATTEMPTS! GTR 90 (
  echo   FAILED: frontend did not start. See logs-web.txt
  goto :fail
)
timeout /t 2 /nobreak >nul
curl -s -o nul -w "" %WEB_URL% >nul 2>&1
if errorlevel 1 goto :waitweb
echo   Frontend        : responding

REM ---- 8. Browser ---------------------------------------------------------
echo [8/8] Opening %WEB_URL% ...
start "" %WEB_URL%

echo.
echo Running. Use stop-local.bat to shut down, check-local.bat to diagnose.
endlocal
exit /b 0

:fail
echo.
echo Startup aborted.
endlocal
exit /b 1

@echo off
REM ===========================================================================
REM  run.bat - one-click local launcher (PostgreSQL + API + frontend + browser)
REM
REM  Safe to run repeatedly: anything already running is reused, never restarted.
REM  NEVER drops a database or deletes data (migrations apply forward only).
REM  Stop everything with stop-local.bat.
REM ===========================================================================
setlocal EnableDelayedExpansion
set "ROOT=%~dp0"
set "PG_BIN=C:\pg-inventory-system\pgsql\bin"
set "PG_DATA=C:\pg-inventory-system\data"
set "API_URL=http://localhost:5165"
set "WEB_URL=http://localhost:3000"

echo ============================================================
echo  Inventory system - starting local environment
echo ============================================================

REM ---- 1. Tools -----------------------------------------------------------
where dotnet >nul 2>&1 || (echo FAILED: dotnet not found on PATH. & goto :fail)
where node   >nul 2>&1 || (echo FAILED: node not found on PATH. & goto :fail)

REM ---- 2. PostgreSQL (portable, port 5433) --------------------------------
echo [1/5] PostgreSQL...
netstat -an | findstr /C:":5433 " | findstr LISTENING >nul 2>&1
if not errorlevel 1 goto :pgready
if not exist "%PG_BIN%\pg_ctl.exe" (
  echo FAILED: %PG_BIN%\pg_ctl.exe not found. Install/restore the portable PostgreSQL first.
  goto :fail
)
echo   starting PostgreSQL on 5433...
REM Detached on purpose: waiting on pg_ctl itself can hang because the server inherits this console.
start "PostgreSQL" /min cmd /c ""%PG_BIN%\pg_ctl.exe" start -D "%PG_DATA%" -l "C:\pg-inventory-system\logfile.log" -o "-p 5433""
set /a N=0
:waitpg
set /a N+=1
if !N! GTR 40 (echo FAILED: PostgreSQL did not start. See C:\pg-inventory-system\logfile.log & goto :fail)
ping -n 3 127.0.0.1 >nul
netstat -an | findstr /C:":5433 " | findstr LISTENING >nul 2>&1
if errorlevel 1 goto :waitpg
:pgready
echo   PostgreSQL: running on 5433

REM ---- 3. Migrations ------------------------------------------------------
echo [2/5] Applying database migrations (forward only)...
netstat -an | findstr /C:":5165 " | findstr LISTENING >nul 2>&1
if errorlevel 1 (
  dotnet ef database update --project "%ROOT%src\Inventory.Infrastructure" --startup-project "%ROOT%src\Inventory.Api" >"%ROOT%logs-migrate.txt" 2>&1
  if errorlevel 1 (
    echo FAILED: migrations did not apply. See logs-migrate.txt
    goto :fail
  )
  echo   Migrations: up to date
) else (
  echo   API already running - migrations skipped
)

REM ---- 4. API -------------------------------------------------------------
echo [3/5] API...
netstat -an | findstr /C:":5165 " | findstr LISTENING >nul 2>&1
if errorlevel 1 (
  start "Inventory.Api" /min "%ROOT%_run-api.cmd"
)
set /a N=0
:waitapi
set /a N+=1
if !N! GTR 90 (echo FAILED: API did not become healthy. See logs-api.txt & goto :fail)
curl -s -f -o nul "%API_URL%/health" >nul 2>&1
if errorlevel 1 (ping -n 3 127.0.0.1 >nul & goto :waitapi)
echo   API: healthy at %API_URL%

REM ---- 5. Frontend --------------------------------------------------------
echo [4/5] Frontend...
if not exist "%ROOT%frontend\node_modules" (
  echo   installing frontend packages ^(first run^)...
  call npm install --prefix "%ROOT%frontend" >"%ROOT%logs-npm.txt" 2>&1
)
netstat -an | findstr /C:":3000 " | findstr LISTENING >nul 2>&1
if errorlevel 1 (
  start "Inventory.Web" /min "%ROOT%_run-web.cmd"
)
set /a N=0
:waitweb
set /a N+=1
if !N! GTR 90 (echo FAILED: frontend did not start. See logs-web.txt & goto :fail)
curl -s -o nul "%WEB_URL%" >nul 2>&1
if errorlevel 1 (ping -n 3 127.0.0.1 >nul & goto :waitweb)
echo   Frontend: running at %WEB_URL%

REM ---- 6. Browser ---------------------------------------------------------
echo [5/5] Opening browser...
start "" "%WEB_URL%"

echo.
echo ============================================================
echo  READY   %WEB_URL%   (API: %API_URL%)
echo  Logs: logs-api.txt / logs-web.txt   Stop: stop-local.bat
echo ============================================================
endlocal
exit /b 0

:fail
echo.
echo Startup aborted.
endlocal
exit /b 1

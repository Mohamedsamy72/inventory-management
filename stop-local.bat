@echo off
REM ===========================================================================
REM  stop-local.bat - Graceful shutdown.   docs/19 section 2.3
REM  Stops ONLY the windows this launcher started, by window title. It does not
REM  kill every dotnet.exe or node.exe on the machine - a developer's editor,
REM  other services and unrelated work are left alone.
REM  It never touches the database or any data directory.
REM ===========================================================================

setlocal
echo Stopping local development processes...

taskkill /FI "WINDOWTITLE eq Inventory.Api*" /T /F >nul 2>&1 && (
  echo   Inventory.Api : stopped
) || echo   Inventory.Api : not running

taskkill /FI "WINDOWTITLE eq Inventory.Web*" /T /F >nul 2>&1 && (
  echo   Inventory.Web : stopped
) || echo   Inventory.Web : not running

echo.
echo PostgreSQL was not started by this launcher and has been left running.
endlocal
exit /b 0

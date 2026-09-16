@echo off
REM ===========================================================================
REM  check-local.bat - Layered diagnostic.   docs/19 section 2.2
REM  Reports a discrete status per layer rather than one generic failure, so
REM  that "it does not work" becomes "PostgreSQL is not listening".
REM  Read-only: starts nothing, stops nothing, changes nothing.
REM ===========================================================================

setlocal EnableDelayedExpansion
set "ROOT=%~dp0"

echo ============================================================
echo  Local environment diagnostic
echo ============================================================
echo.

echo [Toolchain]
where dotnet >nul 2>&1 && (
  for /f "delims=" %%v in ('dotnet --version') do echo   dotnet        : OK  %%v
) || echo   dotnet        : MISSING from PATH
where node >nul 2>&1 && (
  for /f "delims=" %%v in ('node --version') do echo   node          : OK  %%v
) || echo   node          : MISSING from PATH  ^(installed at D:\Nodejs^)
where npm >nul 2>&1 && echo   npm           : OK || echo   npm           : MISSING from PATH
where git >nul 2>&1 && echo   git           : OK || echo   git           : MISSING from PATH
echo.

echo [PostgreSQL]
where psql >nul 2>&1 && (
  for /f "delims=" %%v in ('psql --version') do echo   psql          : OK  %%v
) || echo   psql          : NOT INSTALLED  ^(required 16+ - see docs/33^)
netstat -an | findstr /C:"127.0.0.1:5432" /C:"0.0.0.0:5432" >nul 2>&1 && (
  echo   port 5432     : listening
) || echo   port 5432     : not listening
echo.

echo [Backend API]
curl -s -o nul %~n0 >nul 2>&1
netstat -an | findstr /C:":5165" >nul 2>&1 && (
  echo   port 5165     : listening
) || echo   port 5165     : not listening
curl -s http://localhost:5165/health >nul 2>&1 && (
  echo   /health       : responding
) || echo   /health       : no response
curl -s http://localhost:5165/health/ready >nul 2>&1 && (
  for /f "delims=" %%s in ('curl -s http://localhost:5165/health/ready') do echo   /health/ready : %%s
) || echo   /health/ready : no response
echo.

echo [Frontend]
netstat -an | findstr /C:":3000" >nul 2>&1 && (
  echo   port 3000     : listening
) || echo   port 3000     : not listening
if exist "%ROOT%frontend\node_modules" (
  echo   node_modules  : present
) else (
  echo   node_modules  : MISSING - run: npm install --prefix frontend
)
echo.

echo [Migrations]
echo   no migration exists until Phase 2 - nothing to apply
echo.

echo ============================================================
endlocal
exit /b 0

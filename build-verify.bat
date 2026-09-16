@echo off
REM ===========================================================================
REM  build-verify.bat - Phase verification runner.
REM
REM  Runs every verification command from docs/09-implementation-plan.md
REM  section 8 and writes the literal output to build-verification.log.
REM
REM  WHY THIS EXISTS: command execution from the assisting session to this
REM  machine is unavailable (docs/33 section 7.2), so this script is the
REM  verification channel. Double-click it; the log is read back and the
REM  failures fixed.
REM
REM  It builds and tests. It installs nothing and starts no server.
REM ===========================================================================

setlocal EnableDelayedExpansion
set "ROOT=%~dp0"
set "LOG=%ROOT%build-verification.log"

>  "%LOG%" echo ===========================================================================
>> "%LOG%" echo  PHASE VERIFICATION LOG
>> "%LOG%" echo  Spec: docs/09-implementation-plan.md section 8
>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo  Machine   : %COMPUTERNAME%
>> "%LOG%" echo  Timestamp : %DATE% %TIME%
>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo.

set "FAILED="

echo [1/7] dotnet restore ...
>> "%LOG%" echo --- $ dotnet restore InventorySystem.sln -------------------------------
dotnet restore "%ROOT%InventorySystem.sln" >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! restore" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [2/7] dotnet build -warnaserror ...
>> "%LOG%" echo --- $ dotnet build InventorySystem.sln -warnaserror --------------------
dotnet build "%ROOT%InventorySystem.sln" -warnaserror --no-restore >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! build" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [3/7] dotnet test ...
>> "%LOG%" echo --- $ dotnet test InventorySystem.sln ----------------------------------
dotnet test "%ROOT%InventorySystem.sln" --no-build --verbosity normal >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! test" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [4/7] frontend install ...
>> "%LOG%" echo --- $ npm ci --prefix frontend ----------------------------------------
call npm ci --prefix "%ROOT%frontend" --no-audit --no-fund >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! npm-ci" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [5/7] frontend typecheck ...
>> "%LOG%" echo --- $ npm run typecheck --prefix frontend ------------------------------
call npm run typecheck --prefix "%ROOT%frontend" >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! typecheck" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [6/7] frontend lint + unit tests ...
>> "%LOG%" echo --- $ npm run lint --prefix frontend -----------------------------------
call npm run lint --prefix "%ROOT%frontend" >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! lint" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.
>> "%LOG%" echo --- $ npm run test --prefix frontend -----------------------------------
call npm run test --prefix "%ROOT%frontend" >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! vitest" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

echo [7/7] frontend build ...
>> "%LOG%" echo --- $ npm run build --prefix frontend ----------------------------------
call npm run build --prefix "%ROOT%frontend" >> "%LOG%" 2>&1
if errorlevel 1 (set "FAILED=!FAILED! next-build" & echo    FAILED) else (echo    ok)
>> "%LOG%" echo.

>> "%LOG%" echo ===========================================================================
if "!FAILED!"=="" (
  >> "%LOG%" echo  RESULT: ALL CHECKS PASSED
  echo.
  echo  ALL CHECKS PASSED
) else (
  >> "%LOG%" echo  RESULT: FAILED -!FAILED!
  echo.
  echo  FAILED -!FAILED!
)
>> "%LOG%" echo ===========================================================================

echo.
echo Log written to: %LOG%
echo Leave it in place - it is read back and the failures fixed.
echo.
pause
endlocal
exit /b 0

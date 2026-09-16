@echo off
REM ===========================================================================
REM  Phase 1 - Task 1.1 : Environment Verification
REM  Specification: docs/33-verified-environment-matrix.md  (section 6)
REM
REM  WHAT THIS DOES:  runs each verification command and writes the literal
REM                   output to  env-verification.log  next to this file.
REM  WHAT IT DOES NOT DO: it installs nothing, changes nothing, and touches
REM                   no project file. It is read-only except for the log.
REM
REM  HOW TO RUN:      double-click this file, or run it from a terminal.
REM ===========================================================================

setlocal EnableDelayedExpansion
set "LOG=%~dp0env-verification.log"

> "%LOG%" echo ===========================================================================
>> "%LOG%" echo  PHASE 1 - TASK 1.1 : ENVIRONMENT VERIFICATION LOG
>> "%LOG%" echo  Spec: docs/33-verified-environment-matrix.md section 6
>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo  Machine      : %COMPUTERNAME%
>> "%LOG%" echo  User         : %USERNAME%
>> "%LOG%" echo  Timestamp    : %DATE% %TIME%
>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo.

call :RUN "dotnet --version"
call :RUN "dotnet --list-sdks"
call :RUN "dotnet --list-runtimes"
call :RUN "dotnet ef --version"
call :RUN "node --version"
call :RUN "npm --version"
call :RUN "psql --version"
call :RUN "git --version"
call :RUN "docker --version"

>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo  PATH RESOLUTION  (bare-command availability)
>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo.

call :RUN "where dotnet"
call :RUN "where node"
call :RUN "where npm"
call :RUN "where psql"
call :RUN "where git"
call :RUN "where docker"

>> "%LOG%" echo ===========================================================================
>> "%LOG%" echo  END OF LOG
>> "%LOG%" echo ===========================================================================

echo.
echo Verification complete.
echo Log written to: %LOG%
echo.
echo Please leave the log file in place - it is read back and recorded
echo verbatim into docs/33-verified-environment-matrix.md section 7.
echo.
pause
exit /b 0

REM ---------------------------------------------------------------------------
:RUN
set "CMD=%~1"
>> "%LOG%" echo ---------------------------------------------------------------------------
>> "%LOG%" echo $ !CMD!
>> "%LOG%" echo ---------------------------------------------------------------------------
%CMD% >> "%LOG%" 2>&1
if errorlevel 1 (
  >> "%LOG%" echo [exit code: !errorlevel!  -- command failed or was not found]
)
>> "%LOG%" echo.
exit /b 0

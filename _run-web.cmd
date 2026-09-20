@echo off
REM Started by run.bat - runs the Next.js dev server and keeps output in logs-web.txt.
cd /d "%~dp0frontend"
npm run dev > "..\logs-web.txt" 2>&1

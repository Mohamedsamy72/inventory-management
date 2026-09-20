@echo off
REM Started by run.bat - runs the API and keeps its console output in logs-api.txt.
cd /d "%~dp0"
dotnet run --project "src\Inventory.Api" > "logs-api.txt" 2>&1

@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

echo Validating generated Luban table data...
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%test_tables.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%NO_PAUSE%"=="1" pause
exit /b %EXIT_CODE%

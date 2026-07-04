@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI"

echo Exporting Luban tables...
echo Project root: %ROOT%
call "%ROOT%\Tools\gen_luban.cmd" %*
set "EXIT_CODE=%ERRORLEVEL%"

if "%EXIT_CODE%"=="0" (
  echo.
  echo Export complete.
  echo Code: %ROOT%\Assets\Scripts\Generated\Luban
  echo Data: %ROOT%\Assets\Resources\Config\Luban\Bin
) else (
  echo.
  echo Export failed. Exit code: %EXIT_CODE%
)

if not "%NO_PAUSE%"=="1" pause
exit /b %EXIT_CODE%

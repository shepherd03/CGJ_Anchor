@echo off
setlocal
set ROOT=%~dp0..
pushd "%ROOT%"
call "%ROOT%\Tools\luban.cmd" ^
  --conf "%ROOT%\Config\Luban\luban.conf" ^
  -t client ^
  -c cs-bin ^
  -d bin ^
  -x outputCodeDir=Assets/Scripts/Generated/Luban ^
  -x outputDataDir=Assets/Resources/Config/Luban/Bin ^
  %*
set EXIT_CODE=%ERRORLEVEL%
popd
exit /b %EXIT_CODE%

@echo off
setlocal

set "BUNDLED_EXE=%~dp0esptool.exe"
if exist "%BUNDLED_EXE%" (
  "%BUNDLED_EXE%" %*
  exit /b %errorlevel%
)

python -m esptool %*
exit /b %errorlevel%

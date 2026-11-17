@echo off
set LOG=%TEMP%\LocalRecorder-install.log
echo ==== %DATE% %TIME% > "%LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" >> "%LOG%" 2>&1
echo EXIT %ERRORLEVEL% >> "%LOG%"
exit /b %ERRORLEVEL%

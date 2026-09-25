@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Fallback-Tunnel.ps1"
if errorlevel 1 pause
exit /b %ERRORLEVEL%

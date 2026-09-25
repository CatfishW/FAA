@echo off
setlocal
cd /d "%~dp0"
if not exist "FAA-XR3.exe" (
  echo FAA-XR3.exe is missing. Extract the complete ZIP first.
  pause
  exit /b 1
)
echo Start Varjo Base 4.14 and verify that your XR-3 is ready before launching.
if exist "%~dp0Check-Terrain.cmd" call "%~dp0Check-Terrain.cmd"
if errorlevel 1 echo Terrain is not connected yet. The flight-data connection alone does not supply terrain.
echo Log: %LOCALAPPDATA%\FAA-XR3\Player-XR3.log
if not exist "%LOCALAPPDATA%\FAA-XR3" mkdir "%LOCALAPPDATA%\FAA-XR3"
"%~dp0FAA-XR3.exe" --xr3 -force-d3d11 -logFile "%LOCALAPPDATA%\FAA-XR3\Player-XR3.log" %*
exit /b %ERRORLEVEL%

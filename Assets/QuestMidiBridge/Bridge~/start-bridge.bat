@echo off
setlocal
cd /d "%~dp0"
title Quest MIDI Bridge
echo ===============================================
echo    Quest MIDI Bridge
echo ===============================================
echo.

where node >nul 2>nul
if errorlevel 1 (
  echo ERROR: Node.js was not found on your PATH.
  echo Install it from https://nodejs.org  then run this again.
  echo.
  pause
  exit /b 1
)

if not exist node_modules (
  echo Installing dependencies ^(first run only, ~30s^)...
  echo.
  call npm install
  if errorlevel 1 (
    echo.
    echo npm install failed - see the messages above.
    pause
    exit /b 1
  )
  echo.
)

node bridge.js

echo.
echo Bridge stopped.
pause

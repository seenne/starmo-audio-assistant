@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\pack-portable.ps1"
if errorlevel 1 (
  echo Packaging failed.
  exit /b %errorlevel%
)
echo Packaging complete.

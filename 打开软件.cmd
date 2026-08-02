@echo off
set "APP=%~dp0dist\GameTranslator.exe"
if not exist "%APP%" (
  echo GameTranslator.exe not found.
  echo Run build.ps1 first.
  pause
  exit /b 1
)
start "" "%APP%"

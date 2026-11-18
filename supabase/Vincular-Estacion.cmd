@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Vincular-Estacion.ps1"
set "resultado=%errorlevel%"
pause
exit /b %resultado%

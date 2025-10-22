@echo off
echo Building SrtExtractor Installer...
powershell -ExecutionPolicy Bypass -File "scripts\build-installer.ps1" %*
pause

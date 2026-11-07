@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-MakroPlus.ps1"
if errorlevel 1 pause

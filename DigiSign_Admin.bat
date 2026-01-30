@echo off
REM DigiSign Admin Mode Launcher
REM This forces a new proper console window with stdin attached

REM Change to the script's directory
cd /d "%~dp0"

REM Start a NEW cmd.exe window that will run the admin mode
REM This ensures stdin/stdout are properly attached to the console
start "DigiSign Admin Mode" /wait cmd /k "DigiSign.exe /admin & pause & exit"

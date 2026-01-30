@echo off
echo ===============================================================
echo   Admin Mode Diagnostic Test
echo ===============================================================
echo.
echo This script will help diagnose the prompt display issue.
echo.
echo Test 1: Running admin mode directly
echo ---------------------------------------------------------------
echo.
echo Press any key to start admin mode...
pause >nul
echo.
echo Starting: DigiSign.exe /admin
echo.
DigiSign.exe /admin
echo.
echo ===============================================================
echo.
echo Did you see the "Path: " prompt? (Y/N)
set /p RESULT="Answer: "
echo.
if /i "%RESULT%"=="Y" (
    echo ? Good! The prompt is displaying correctly.
) else (
    echo ? Issue confirmed. The prompt is not displaying.
    echo.
    echo Checking application_log.txt for clues...
    echo.
    if exist application_log.txt (
        echo Last 10 lines of application_log.txt:
        echo ---------------------------------------------------------------
        powershell -Command "Get-Content application_log.txt -Tail 10"
    ) else (
        echo ?? application_log.txt not found
    )
)
echo.
echo ===============================================================
echo.
pause

@echo off
echo ===============================================================
echo   Compiling Admin Key Tester...
echo ===============================================================
echo.

csc /out:AdminKeyTester.exe AdminKeyTester.cs

if %ERRORLEVEL% EQU 0 (
    echo ? Compilation successful!
    echo.
    echo ===============================================================
    echo   Running Admin Key Tester...
    echo ===============================================================
    echo.
    AdminKeyTester.exe
    
    echo.
    echo ===============================================================
    echo.
    echo If admin.license was created above, copy it to:
    echo    D:\Development\DigiSign\
    echo.
    echo Then test with:
    echo    cd D:\Development\DigiSign
    echo    DigiSign.exe /admin
    echo ===============================================================
    
    REM Clean up exe
    if exist AdminKeyTester.exe del AdminKeyTester.exe
) else (
    echo ? Compilation failed!
    echo.
    echo Please ensure .NET Framework SDK is installed.
    echo Or compile using Visual Studio.
)

echo.
pause

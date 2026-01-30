@echo off
setlocal enabledelayedexpansion

echo.
echo ===============================================================
echo   DigiSign Admin License - Complete Solution
echo ===============================================================
echo.

REM Step 1: Compile AdminKeyTester
echo [1/4] Compiling AdminKeyTester...
csc /out:AdminKeyTester.exe AdminKeyTester.cs 2>nul

if %ERRORLEVEL% NEQ 0 (
    echo ? Failed to compile AdminKeyTester.cs
    echo.
    echo Please ensure .NET Framework SDK is installed.
    echo You can also use Visual Studio to compile the .cs files.
    pause
    exit /b 1
)
echo ? AdminKeyTester compiled successfully
echo.

REM Step 2: Generate correct admin.license
echo [2/4] Generating correct admin.license file...
AdminKeyTester.exe >nul
if exist admin.license (
    echo ? admin.license generated successfully
) else (
    echo ? Failed to generate admin.license
    pause
    exit /b 1
)
echo.

REM Step 3: Compile AdminLicenseValidator
echo [3/4] Compiling AdminLicenseValidator...
csc /out:AdminLicenseValidator.exe AdminLicenseValidator.cs 2>nul

if %ERRORLEVEL% NEQ 0 (
    echo ??  Validator compilation failed, skipping validation
    goto :CopyFile
)
echo ? AdminLicenseValidator compiled successfully
echo.

REM Step 4: Validate the generated file
echo [4/4] Validating generated admin.license...
echo.
AdminLicenseValidator.exe admin.license
echo.

:CopyFile
echo ===============================================================
echo   NEXT STEPS:
echo ===============================================================
echo.
echo 1. Copy the generated admin.license file to:
echo    D:\Development\DigiSign\
echo.
echo 2. Test it by running:
echo    cd D:\Development\DigiSign
echo    DigiSign.exe /admin
echo.
echo 3. You should see: "? Admin license validated"
echo.
echo ===============================================================
echo.

REM Offer to copy the file automatically
set /p COPY="Do you want to copy admin.license to D:\Development\DigiSign now? (Y/N): "
if /i "%COPY%"=="Y" (
    if exist "D:\Development\DigiSign" (
        copy /Y admin.license "D:\Development\DigiSign\admin.license" >nul
        if %ERRORLEVEL% EQU 0 (
            echo.
            echo ? File copied successfully to D:\Development\DigiSign\
            echo.
            echo Run this command to test:
            echo    cd D:\Development\DigiSign
            echo    DigiSign.exe /admin
        ) else (
            echo.
            echo ? Failed to copy file. Please copy manually.
        )
    ) else (
        echo.
        echo ? Directory D:\Development\DigiSign not found!
        echo    Please copy admin.license manually.
    )
)

echo.
echo ===============================================================
echo   Generated Files Summary:
echo ===============================================================
echo.
type admin.license
echo.
echo ===============================================================

REM Clean up
if exist AdminKeyTester.exe del AdminKeyTester.exe
if exist AdminLicenseValidator.exe del AdminLicenseValidator.exe

echo.
pause

@echo off
REM DigiSign Merge Helper Script
REM This script assists with merging master into digisign-prod
REM Execute from: D:\Development\DigiSign

echo ========================================
echo DigiSign Merge Helper
echo ========================================
echo.

REM Check current directory
if not exist ".git" (
    echo ERROR: Not in a git repository!
    echo Please run this from D:\Development\DigiSign
    pause
    exit /b 1
)

:MENU
echo.
echo Select an option:
echo.
echo 1. Verify current state
echo 2. Fetch latest changes
echo 3. Create backup branch
echo 4. Execute merge
echo 5. Check merge status
echo 6. Abort merge (emergency)
echo 7. Verify signing files unchanged
echo 8. Build solution
echo 9. Complete merge
echo 0. Exit
echo.
set /p choice="Enter choice (0-9): "

if "%choice%"=="1" goto VERIFY
if "%choice%"=="2" goto FETCH
if "%choice%"=="3" goto BACKUP
if "%choice%"=="4" goto MERGE
if "%choice%"=="5" goto STATUS
if "%choice%"=="6" goto ABORT
if "%choice%"=="7" goto VERIFY_FILES
if "%choice%"=="8" goto BUILD
if "%choice%"=="9" goto COMPLETE
if "%choice%"=="0" goto END
goto MENU

:VERIFY
echo.
echo === Verifying Current State ===
git branch --show-current
echo.
git status
pause
goto MENU

:FETCH
echo.
echo === Fetching Latest Changes ===
git fetch --all
echo.
echo Commits in master not in digisign-prod:
git log digisign-prod..origin/master --oneline
echo.
echo Commits in digisign-prod not in master:
git log origin/master..digisign-prod --oneline
pause
goto MENU

:BACKUP
echo.
echo === Creating Backup Branch ===
for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set BACKUP_DATE=%%c%%a%%b)
set BACKUP_BRANCH=digisign-prod-backup-%BACKUP_DATE%
git branch %BACKUP_BRANCH%
echo Created backup branch: %BACKUP_BRANCH%
git branch -a
pause
goto MENU

:MERGE
echo.
echo === Executing Merge ===
echo.
echo WARNING: This will merge master into digisign-prod
set /p confirm="Continue? (Y/N): "
if /i not "%confirm%"=="Y" goto MENU

git merge origin/master --no-ff -m "Merge master updates into digisign-prod"
echo.
echo Merge attempted. Check status above.
echo If there are conflicts, resolve them manually.
pause
goto MENU

:STATUS
echo.
echo === Merge Status ===
git status
echo.
echo Conflicted files (if any):
git diff --name-only --diff-filter=U
pause
goto MENU

:ABORT
echo.
echo === Aborting Merge ===
echo.
echo WARNING: This will abort the current merge!
set /p confirm="Are you sure? (Y/N): "
if /i not "%confirm%"=="Y" goto MENU

git merge --abort
echo Merge aborted.
pause
goto MENU

:VERIFY_FILES
echo.
echo === Verifying Signing Files ===
echo.
echo Checking critical files for changes...
echo.

for /f "tokens=2-4 delims=/ " %%a in ('date /t') do (set BACKUP_DATE=%%c%%a%%b)
set BACKUP_BRANCH=digisign-prod-backup-%BACKUP_DATE%

echo Checking SignatureHelper.cs...
git diff %BACKUP_BRANCH% SignatureHelper.cs
echo.

echo Checking DigitalSignatureService.cs...
git diff %BACKUP_BRANCH% DigitalSignatureService.cs
echo.

echo Checking X509Certificate2Extension.cs...
git diff %BACKUP_BRANCH% X509Certificate2Extension.cs
echo.

echo Checking SignatureConfiguration.cs...
git diff %BACKUP_BRANCH% SignatureConfiguration.cs
echo.

echo Checking packages.config...
git diff %BACKUP_BRANCH% packages.config
echo.

echo If no output above, files are unchanged (GOOD!)
pause
goto MENU

:BUILD
echo.
echo === Building Solution ===
echo.
echo Opening Visual Studio to build...
echo Please build manually in Visual Studio.
echo Build -^> Rebuild Solution
start DigiSign.sln
pause
goto MENU

:COMPLETE
echo.
echo === Complete Merge ===
echo.
echo This will finalize the merge.
echo Make sure you have:
echo   - Resolved all conflicts
echo   - Verified signing files unchanged
echo   - Built successfully
echo   - Tested signing functionality
echo.
set /p confirm="Ready to complete merge? (Y/N): "
if /i not "%confirm%"=="Y" goto MENU

git status
echo.
echo If everything looks good, the merge is complete!
echo.
echo Optional: Push to remote with:
echo   git push origin digisign-prod
pause
goto MENU

:END
echo.
echo Exiting...
exit /b 0

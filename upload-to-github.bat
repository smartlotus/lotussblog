@echo off
chcp 65001 >nul
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%scripts\publish-to-github.ps1"

echo.
echo Exporting the current site as a standalone project and pushing it to GitHub...
echo Default repository: https://github.com/smartlotus/lotussblog.git
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
    echo Finished.
) else (
    echo Failed. Please review the error message above.
)

echo.
pause
exit /b %EXIT_CODE%

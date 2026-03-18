@echo off
setlocal

set "ROOT=%~dp0"
set "SCRIPT=%ROOT%tools\CardAssistant\card-assistant.ps1"

if not exist "%SCRIPT%" (
    echo.
    echo Cannot find script:
    echo %SCRIPT%
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
if errorlevel 1 (
    echo.
    echo Card Assistant exited with an error.
    pause
    exit /b 1
)

exit /b 0

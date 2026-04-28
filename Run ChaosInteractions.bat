@echo off
rem Startup launcher for ChaosInteractions
rem Double-click this file to build (if needed) and run the app.

pushd "%~dp0"
set "exe=bin\Debug\net8.0-windows\ChaosInteractions.exe"

if exist "%exe%" (
    start "ChaosInteractions" "%exe%"
    popd
    exit /b 0
)

echo Executable not found. Building project...
if not exist "ChaosInteractions.csproj" (
    echo ERROR: Could not find ChaosInteractions.csproj in %~dp0
    pause
    popd
    exit /b 1
)

dotnet build "ChaosInteractions.csproj" --configuration Debug
if errorlevel 1 (
    echo Build failed.
    pause
    popd
    exit /b 1
)

if exist "%exe%" (
    start "ChaosInteractions" "%exe%"
) else (
    echo ERROR: Built executable not found at %exe%
    pause
    popd
    exit /b 1
)

popd

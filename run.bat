@echo off
setlocal enabledelayedexpansion
title SmartVideoOptimizer - Desktop Studio

:: Ensure current working directory is always this script's directory
cd /d "%~dp0"

echo [SmartVideoOptimizer] Starting desktop application (.NET 10 LTS)...

if exist "%~dp0SmartVideoOptimizer.exe" (
    start "" "%~dp0SmartVideoOptimizer.exe"
    exit /b 0
)

if exist "%~dp0SmartVideoOptimizer.App.exe" (
    start "" "%~dp0SmartVideoOptimizer.App.exe"
    exit /b 0
)

if exist "%~dp0src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe" (
    start "" "%~dp0src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe"
    exit /b 0
)

if exist "%~dp0src\SmartVideoOptimizer.App\bin\Debug\net10.0-windows\SmartVideoOptimizer.App.exe" (
    start "" "%~dp0src\SmartVideoOptimizer.App\bin\Debug\net10.0-windows\SmartVideoOptimizer.App.exe"
    exit /b 0
)

echo [SmartVideoOptimizer] Compiling solution in Release mode...
dotnet build "%~dp0SmartVideoOptimizer.slnx" -c Release

if exist "%~dp0src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe" (
    start "" "%~dp0src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe"
) else (
    echo [SmartVideoOptimizer] Error: Could not launch application.
    pause
)
exit /b 0

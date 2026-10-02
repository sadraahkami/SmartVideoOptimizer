@echo off
setlocal
title SmartVideoOptimizer - Launching Application
echo [SmartVideoOptimizer] Starting desktop application (.NET 10 LTS)...

if exist "src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe" (
    start "" "src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe"
) else if exist "src\SmartVideoOptimizer.App\bin\Debug\net10.0-windows\SmartVideoOptimizer.App.exe" (
    start "" "src\SmartVideoOptimizer.App\bin\Debug\net10.0-windows\SmartVideoOptimizer.App.exe"
) else (
    echo [SmartVideoOptimizer] Executable not found. Compiling first...
    dotnet build src/SmartVideoOptimizer.App -c Release
    start "" "src\SmartVideoOptimizer.App\bin\Release\net10.0-windows\SmartVideoOptimizer.App.exe"
)
exit /b 0

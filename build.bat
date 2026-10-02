@echo off
setlocal
title SmartVideoOptimizer - Building Solution
echo [SmartVideoOptimizer] Compiling full solution with .NET 10 LTS...

dotnet build SmartVideoOptimizer.slnx -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [SmartVideoOptimizer] Build failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

echo [SmartVideoOptimizer] Build succeeded successfully.
exit /b 0

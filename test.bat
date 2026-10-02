@echo off
setlocal
title SmartVideoOptimizer - Running Automated Tests
echo [SmartVideoOptimizer] Running automated unit and integration tests...

dotnet test SmartVideoOptimizer.slnx --logger "console;verbosity=normal"
if %ERRORLEVEL% NEQ 0 (
    echo [SmartVideoOptimizer] Tests failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

echo [SmartVideoOptimizer] All tests passed!
exit /b 0

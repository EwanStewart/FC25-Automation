@echo off
set "exePath=C:\dev\FC25-Automation\Automation\bin\Debug\net9.0\Automation.exe"
if exist "%exePath%" (
    echo Starting Automation.exe...
    start "" "%exePath%"
    echo Automation.exe started successfully.
) else (
    echo Error: Automation.exe not found at %exePath%
    pause
)
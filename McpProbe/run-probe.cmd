@echo off
setlocal

REM Build and publish the server if needed
dotnet publish ..\MyAiBuiltProject -c Release -o ..\MyAiBuiltProject\bin\Release\net9.0\publish
if errorlevel1 exit /b1

REM Run the probe
dotnet run --project . %*

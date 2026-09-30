@echo off
set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
start "" "%~dp0bin\Release\net8.0-windows\win-x64\publish\PosElectro.Desktop.exe"

@echo off
title Line Kassa
echo Line Kassa ishga tushirilmoqda...
set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
set PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%
cd /d "%~dp0PROD\LinePOS_Desktop"
start "" "PosElectro.Desktop.exe"
exit

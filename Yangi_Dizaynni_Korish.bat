@echo off
chcp 65001 >nul
title Line kassa - yangi dizayn (TEST)
rem Koddagi eng oxirgi o'zgarishlarni ko'rish uchun: dasturni koddan yig'adi va ALOHIDA test bazasi bilan ochadi.
rem Haqiqiy ishchi baza (%LOCALAPPDATA%\PosElectro\pos_desktop.db) faqat bir marta NUSXA olinadi, o'zi o'zgarmaydi.
rem Ishchi kassa uchun esa avvalgidek Ishga_Tushirish.bat ishlatiladi.

set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
set PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%

set "DEVDIR=%LOCALAPPDATA%\PosElectro_Dizayn"
set "REALDB=%LOCALAPPDATA%\PosElectro\pos_desktop.db"
if not exist "%DEVDIR%" mkdir "%DEVDIR%"
if not exist "%DEVDIR%\pos_desktop.db" if exist "%REALDB%" (
  echo Haqiqiy bazadan test uchun nusxa olinmoqda...
  copy /y "%REALDB%" "%DEVDIR%\pos_desktop.db" >nul
)
set "LINEPOS_DB_PATH=%DEVDIR%\pos_desktop.db"

echo Dastur koddan yig'ilmoqda (1-2 daqiqa)...
dotnet build "%~dp0desktop\PosElectro.Desktop\PosElectro.Desktop.csproj" -c Debug -v q -nologo
if errorlevel 1 (
  echo.
  echo Yig'ishda xato chiqdi. Yuqoridagi xabarni dasturchiga yuboring.
  pause
  exit /b 1
)

echo Ochilmoqda. Test bazasi: %LINEPOS_DB_PATH%
echo Eslatma: telefonlarni shu test oynasiga ulamang, aks holda savdolar test bazasiga tushadi.
start "" "%~dp0desktop\PosElectro.Desktop\bin\Debug\net8.0-windows\PosElectro.Desktop.exe"

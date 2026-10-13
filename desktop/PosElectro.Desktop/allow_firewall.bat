@echo off
title Line kassa - Windows Firewall Port 8080 Ruxsati
echo ============================================================
echo   Line kassa Desktop uchun Windows Firewall ruxsati berish
echo ============================================================
echo.
echo Port 8080 (TCP va UDP) uchun ruxsat qoidasi ochilmoqda...
netsh advfirewall firewall delete rule name="Line kassa Desktop Sync" >nul 2>&1
netsh advfirewall firewall add rule name="Line kassa Desktop Sync" dir=in action=allow protocol=TCP localport=8080 profile=private remoteip=localsubnet
if errorlevel 1 goto failed
netsh advfirewall firewall add rule name="Line kassa Desktop Sync" dir=in action=allow protocol=UDP localport=8080 profile=private remoteip=localsubnet
echo.
if %errorlevel% equ 0 (
    echo [Muvaffaqiyatli] Port 8080 muvaffaqiyatli ochildi!
    echo Endi telefon orqali bemalol ulanishingiz mumkin.
) else (
    echo [Diqqat] Ruxsat ochilmadi. Iltimos ushbu faylni "Administrator nomidan ishga tushirish" (Run as administrator) qilib oching.
)
echo.
pause
exit /b
:failed
echo Firewall ruxsati ochilmadi. Administrator nomidan qayta ishga tushiring.
pause
exit /b 1

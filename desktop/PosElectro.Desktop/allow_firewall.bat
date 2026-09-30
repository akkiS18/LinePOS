@echo off
title Line kassa - Windows Firewall Port 8080 Ruxsati
echo ============================================================
echo   Line kassa Desktop uchun Windows Firewall ruxsati berish
echo ============================================================
echo.
echo Port 8080 (TCP) uchun ruxsat qoidasi ochilmoqda...
netsh advfirewall firewall add rule name="Line kassa Desktop Sync" dir=in action=allow protocol=TCP localport=8080
echo.
if %errorlevel% equ 0 (
    echo [Muvaffaqiyatli] Port 8080 muvaffaqiyatli ochildi!
    echo Endi telefon orqali bemalol ulanishingiz mumkin.
) else (
    echo [Diqqat] Ruxsat ochilmadi. Iltimos ushbu faylni "Administrator nomidan ishga tushirish" (Run as administrator) qilib oching.
)
echo.
pause

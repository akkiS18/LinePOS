# 🛍️ LinePOS — Savdo, Kassa va Ombor Ekotizimi

[![Kotlin](https://img.shields.io/badge/Kotlin-2.0.21-purple.svg)](https://kotlinlang.org)
[![Android](https://img.shields.io/badge/Platform-Android%208.0%2B-green.svg)](https://developer.android.com)
[![Desktop](https://img.shields.io/badge/Desktop-.NET%208%20WPF-blue.svg)](https://dotnet.microsoft.com)
[![Compose](https://img.shields.io/badge/UI-Jetpack%20Compose-blueviolet.svg)](https://developer.android.com/jetpack/compose)
[![License](https://img.shields.io/badge/License-Proprietary-red.svg)](#)

**LinePOS** — kichik va o'rta biznes (chakana va ulgurji savdo, elektrotexnika, qurilish mollari, do'konlar) uchun maxsus ishlab chiqilgan ko'p platformali zamonaviy savdo va ombor nazorati tizimi.

Tizim **100% oflayn** ishlash imkoniyatiga ega bo'lib, internet o'chib qolgan vaziyatlarda ham uzluksiz ishlaydi hamda lokal Wi-Fi tarmog'i orqali Windows kompyuter va Android telefonlar o'rtasida ma'lumotlarni simsiz sinxronlashtiradi.

---

## 🏗️ Ekotizim Tarkibi

Loyiha quyidagi asosiy modullardan iborat:

```
POS/
├── app/                  # 📱 Android Mobil Kassa ilovasi (Kotlin + Jetpack Compose + Room)
├── desktop/              # 🖥️ Windows Desktop ilovasi (.NET 8 WPF + SQLite)
├── admin/                # 🛡️ Admin & Fleet Management moduli (Firebase Cloud Realtime)
├── PROD/                 # 📚 Hujjatlar, PDF/HTML qo'llanmalar va tarqatma materiallar
│   └── Instruksiya/      # Foydalanuvchi va o'rnatish qo'llanmalari (HTML, PDF, rasmlar)
├── AGENT.md              # 📝 Texnik xotira va arxitektura o'zgarishlari qaydlari
└── DESKTOP_QOLLANMA.md   # 📖 Desktop versiyaning to'liq foydalanish qo'llanmasi
```

---

## ✨ Asosiy Imkoniyatlar

- **⚡ 100% Oflayn va Mustaqil:** Barcha savdo, ombor va hisob-kitoblar qurilmaning ichki bazasida (Android: Room, Desktop: SQLite) saqlanadi.
- **🔄 Wi-Fi P2P Lokal Sinxronizatsiya:** Windows Desktop va Android mobil kassalar bir xil Wi-Fi routerga ulanganda internet talab qilmasdan real-vaqt rejimida savdolar, yangi tovarlar va ombor qoldiqlarini yangilab boradi.
- **⚠️ Brak / Spisanie (0 so'mga chiqarish):** Yaroqsiz, singan yoki брак tovarlarni savatdan bitta tugma yoki `Backspace` tugmasi orqali 0 so'm qilib hisobdan chiqarish. To'lov dialogi ochilmaydi, chek chiqmaydi, hisobotlarda sof zarar sifatida aniq hisoblanadi.
- **🏷️ Ikki xil narx (1-narx / 2-narx):** Chakana va ulgurji (optom) narxlarni bitta tugma bilan almashtirish imkoniyati.
- **⏸️ Multi-Cart (Kutishga qo'yish / Hold):** Bir vaqtning o'zida bir nechta xaridorga xizmat ko'rsatish, savatchani kutishga olib yangi savat ochish.
- **🔍 Shtrix-kod va Qidiruv:** Kamera (CameraX + ML Kit) va tashqi USB/Bluetooth skanerlarni qo'llab-quvvatlash. Kassada topilmagan yangi tovarlarni to'g'ridan-to'g'ri kassaning o'zidan tezkor kiritish.
- **🖨️ Printer va Shtrix-kod Chop Etish:**
  - 58 mm va 80 mm termal chek printerlari (Bluetooth / USB / LAN).
  - A4 formatida to'liq hisobot va schyot-faktura chop etish.
  - Shtrix-kod stikerlarini termal printerda millimetrik noziklikda chop etish.
- **📊 Moliya va Hisobotlar:** Kunlik tushum, sof foyda/zarar, to'lov turlari (Naqd, Karta, Aralash, Brak) tahlili, Excel formatida tovarlar ro'yxatini eksport qilish.
- **💾 Zaxira Nusxa (Backup):** Ma'lumotlar bazasini Telegram bot (`@SmartKassaBackupBot`) yoki Android Share Sheet orqali bitta bosish bilan zaxiralash.

---

## 🛠 Texnologik Stek

### 📱 Android ilovasi (`app`):
* **Til:** Kotlin 2.0.21
* **UI freymvork:** Jetpack Compose (Material 3, Apple HIG elementlari bilan)
* **Arxitektura:** MVVM + Clean Architecture + Repository Pattern
* **DI:** Dagger Hilt 2.52
* **Mahalliy baza:** Android Room Database 2.6.1 + KSP
* **Skaner:** CameraX 1.4.0 + Google ML Kit Barcode Scanning
* **Xavfsizlik:** ProGuard / R8 Obfuscation & Resource Shrinking

### 🖥️ Windows Desktop ilovasi (`desktop`):
* **Platforma:** .NET 8.0 (Windows x64)
* **UI freymvork:** WPF (Windows Presentation Foundation) Modern Dark Glass Theme
* **Baza:** Microsoft.Data.Sqlite (SQLite)
* **Installer:** Inno Setup Compiler (`Line_kassa_Setup.iss`)
* **Ulanish:** TCP/HTTP Local Wi-Fi Sync Server

---

## 🚀 O'rnatish va Ishga Tushirish

### 📱 Android ilovasi:
1. Loyihani **Android Studio** (Ladybug yoki undan yuqori) orqali oching.
2. `local.properties` faylini yarating va SDK yo'lini ko'rsating:
   ```properties
   sdk.dir=C\:\\Users\\...\\AppData\\Local\\Android\\Sdk
   ADMIN_PIN=2846
   ```
3. Gradle bilan sinxronizatsiya qiling va `app` modulini ishga tushiring:
   ```bash
   ./gradlew assembleDebug
   ```

### 🖥️ Windows Desktop ilovasi:
1. `desktop/PosElectro.Desktop.sln` faylini **Visual Studio 2022** orqali oching.
2. Target: `.NET 8.0-windows`, Platform: `x64` yoki `Any CPU`.
3. Yoki terminal orqali ishga tushiring:
   ```powershell
   dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj
   dotnet run --project desktop/PosElectro.Desktop/PosElectro.Desktop.csproj
   ```

---

## 📄 Hujjatlar

Batafsil qo'llanmalar loyiha ichida mavjud:
- **Desktop Qo'llanma:** [DESKTOP_QOLLANMA.md](DESKTOP_QOLLANMA.md)
- **Vizual Qo'llanma (HTML/PDF):** `PROD/Instruksiya/LinePOS_Desktop_Foydalanish_Qollanmasi.html`
- **Loyiha Xotirasi (Agent Log):** [AGENT.md](AGENT.md)

---

## 🔐 Litsenziya

Barcha huquqlar himoyalangan. Ushbu dasturiy ta'minot mualliflik huquqi bilan himoyalangan.
Ruxsatsiz nusxa ko'chirish, tarqatish yoki tijoriy maqsadlarda foydalanish taqiqlanadi.

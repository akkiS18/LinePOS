# Nol qoldiq amallari — 1-bosqich test natijasi, 2026-10-14

Holat: **qisman tekshirildi; C# integration testi bloklangan**.

## O‘tgan tekshiruvlar

`python tests/check_zero_stock_triggers.py <desktop.db> <mobile.db>`:

- Uch sxema nusxasi bir xil.
- Yuborilgan desktop va mobil zaxiralarning xotiradagi nusxalarida production insert/delete triggerlari o‘rnatildi.
- Har ikkala nusxada nol qoldiq qatorini yaratish/o‘chirish navbatga amal yozmadi.
- Har ikkala nusxada 5 dona kiritish, -2 gacha o‘zgartirish va o‘chirish tegishlicha +5, -7, +2 delta yaratdi. Manfiy qoldiq ruxsati saqlangan.
- Asl ikkala faylning SHA-256 qiymati o‘zgarmadi.

`python tests/test_sync_schema.py`: **7/7 PASS**.

Sinov tayyorlashda umumiy `sync/schema.sql` nusxasi ilovalardagidan ortda qolgani aniqlandi va moslashtirildi. Bu umumiy sxemadan foydalanadigan testlarning eski triggerlarni sinashini bartaraf etadi.

## Hali bajarilmagan tekshiruvlar

`tests/WifiSync.ZeroDeltaTests` production `WifiSyncStore.cs` va desktop sxemasini bevosita ishlatadi. Kiruvchi bazalar ReadOnly ochiladi; desktopning vaqtinchalik nusxasida test ishlaydi. Maxfiy baza fayllari repoga qo‘shilmagan.

Rejalashtirilgan tekshiruvlar: berilgan 7 nol amal ACK, biznes jadvallari o‘zgarmasligi, aynan bir so‘rovni takror yuborish, o‘zgartirilgan payloadni bir xil ID bilan rad etish, nol bo‘lmagan yo‘q tovarni rad etish, aralash paket rollbacki, pull javobida yetishmayotgan qaytarish va chek mavjudligi.

.NET 8.0.408 CLI kodni build qilishdan oldin `System.Diagnostics.Process.GetStat/Get_StartTime` xatosi bilan to‘xtadi. Bevosita MSBuild ham `ProcessName` olishda ichki xato berdi. Shu sabab bu C# testlar **NOT RUN**, compile holati ham tasdiqlanmagan. Python natijalari C# integration PASS o‘rnini bosmaydi.

Lokal .NET 8 SDK muhitida repo ildizidan:

```powershell
dotnet run --project tests/WifiSync.ZeroDeltaTests -- "C:\backups\SMARTKassa_Baza_2026-10-14_1318.db" "C:\backups\LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db"
```

Bu test aynan taqdim etilgan nusxalar uchun 7 navbat amali va 3 desktop qaytarishini kutadi. Boshqa bazada sonlar o‘zgargan bo‘lsa, bu fixture tekshiruvi mos kelmaydi.

Haqiqiy telefon UI, HTTP/Wi-Fi va mobil bazaga pullni qo‘llash bu bosqichda sinovdan o‘tmadi. Main merge va reliz bajarilmadi.

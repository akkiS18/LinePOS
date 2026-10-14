# Test 2-bosqich — build va qaytarish, 2026-10-14

**Holat: qisman; build va runtime testlar BLOCKED.**

## Bajarildi

`tests/check_return_backup_invariants.py` ikkala yuborilgan bazani ReadOnly ochib tekshirdi:

- Desktopdagi 3, mobildagi 2 qaytarishning chek va asl savdo qatorlari mavjud.
- Chek summasi va tannarxi qaytarish qatorlarining teskari yig‘indisiga mos.
- Naqd/karta summalari mos, request GUIDlar takrorlanmagan.
- Bekor qilish yozuvlarining summalari va miqdori asl qaytarishga teskari.
- Jami qaytarilgan miqdor sotilgan miqdordan oshmagan.
- Asl fayllarning SHA-256 qiymatlari o‘zgarmagan.

**Ikkala bazada PASS.** Bu mavjud yozuvlar mosligi; yangi qaytarish operatsiyasining runtime testi emas. Desktopdagi uchinchi chek hali mobil zaxirada yo‘q, chunki bu oldin yuborilgan zaxiralar.

## Build urinishlari

- Android `bash gradlew :app:compileDebugKotlin --offline --console=plain`: Gradle 9.3.0 lokalda yo‘q, wrapper yuklashda `Network is unreachable`. Kotlin kompilyatori ishga tushmadi. Android SDK ham topilmadi.
- Desktop `dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj -p:EnableWindowsTargeting=true --no-restore`: .NET CLI `System.Diagnostics.Process.GetStat/Get_StartTime` ichki xatosi bilan compilerga yetmasdan to‘xtadi.

Bular ilova buildida topilgan kod xatolari emas; shu muhitdagi to‘siqlar. Build SUCCESS yoki runtime PASS deb belgilash mumkin emas.

## Faqat qaytarish testlarini lokal ishga tushirish

`Business.CoreTests`ga `--returns` rejimi qo‘shildi. U mavjud `ReturnAccountingTests`, `ReturnStoreTests`, `DebtReturnAndReversalTests`ni ishlatadi; boshqa hisobot suite’larini boshlamaydi.

Repo ildizidan .NET 8 SDK o‘rnatilgan muhitda:

```powershell
dotnet run --project tests/Business.CoreTests -- --returns
```

Testlar vaqtinchalik bazalarda ishlaydi. Qisman/to‘liq, kasr miqdor, sifatli/brak qaytarish, request replay, parallel limit, qarz offseti, eskirgan quote, reversal va qarz refund/transfer holatlarini qamrab oluvchi mavjud suite’lar tanlanadi. Bu buyruq ushbu muhitda bajarilmadi; test loyihasining kompilyatsiyasi ham hali tasdiqlanmagan.

Ilovalarni build qilish:

```powershell
.\gradlew.bat :app:compileDebugKotlin --console=plain
dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj
```

Android UI crash, desktop oynasi, qurilmalararo HTTP va real takroriy bosishlar ushbu bosqichda tekshirilmagan. Production kodi/UI o‘zgartirilmadi; faqat test tanlash rejimi va tekshiruv skripti qo‘shildi. Main merge yo‘q. 3-bosqich boshlanmadi.

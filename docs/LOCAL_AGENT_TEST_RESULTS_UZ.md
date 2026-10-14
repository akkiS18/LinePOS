# LinePOS — Lokal Agent Test Natijalari (Stage 3 & Regressiya)

**Sana:** 2026-10-14  
**Branch:** `fix/debt-stage3-complete`  
**Oxirgi commit:** `d569fc2` (`fix(debt): fail closed on missing financial sync envelopes`)  
**Ishchi muhit:** Windows 11 x64, .NET SDK 8.0.425, OpenJDK 21.0.10 (JBR), Android SDK 36, Gradle 9.3.0  
**Asl zaxira bazalar:**
- Desktop: `C:\Users\Acer\Downloads\SMARTKassa_Baza_2026-10-14_1318.db`
- Mobil: `C:\Users\Acer\Downloads\LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db`

---

## 0. Muhit va Tayyorgarlik

| Test ID | Holat | Muhit / Tafsilot | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|---|
| **ENV-01** | Git branch sync | Git CLI / Windows | `fix/debt-stage3-complete` ga o'tish va fast-forward pull | `origin/fix/debt-stage3-complete` commit `d569fc2` gacha muvaffaqiyatli yangilandi | **PASS** |
| **ENV-02** | .NET SDK | CLI (`dotnet --info`) | .NET 8 SDK mavjudligi | .NET SDK 8.0.425 (`C:\Users\Acer\AppData\Local\Microsoft\dotnet`) | **PASS** |
| **ENV-03** | Java / Gradle | CLI (`gradlew -v`) | Java 17+ va Gradle 9+ | OpenJDK 21.0.10 (Android Studio JBR) + Gradle 9.3.0 | **PASS** |

---

## 1A. Kompilyatsiya va Build Tekshiruvlari

| Test ID | Komanda | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|
| **BLD-01** | `dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj` | 0 Error, 0 Warning | Build succeeded (0 Error, 0 Warning) | **PASS** |
| **BLD-02** | `.\gradlew.bat :app:compileDebugKotlin :app:compileDebugAndroidTestKotlin --console=plain` | BUILD SUCCESSFUL | 32 tasks (7 executed, 25 up-to-date), BUILD SUCCESSFUL | **PASS** |

---

## 1B. Nol Qoldiq Amali (Zero-Delta Sync) va Triggerlar

| Test ID | Komanda / Qadam | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|
| **ZDEL-01** | `dotnet run --project tests/WifiSync.ZeroDeltaTests -- "<desktop_db>" "<mobile_db>"` | 7 ACK, replay idempotent, noto'g'ri payload/missing product rad etilishi, missing return pull | Barcha 9 ta assertion to'liq PASS (7 ACK, 0 delta stock o'zgarmasligi, missing return `64f35e30` topilishi) | **PASS** |
| **ZDEL-02** | `python tests/check_zero_stock_triggers.py "<desktop_db>" "<mobile_db>"` | 0-delta insert/delete da journal yozilmasligi, zaxiralar o'zgarmasligi | 7 ta tekshiruv PASS (uchala schema mos, 0 insert/delete journalga tushmaydi, manfiy qoldiq saqlanadi) | **PASS** |
| **ZDEL-03** | `python tests/test_sync_schema.py` | 7 ta sxema testi | Ran 7 tests in 0.013s — OK | **PASS** |
| **ZDEL-04** | Haqiqiy mobil va desktop ilovalari o'rtasida live zero-delta sync | Jonli sinxron paytida 0-delta navbat tozalanishi | Jismoniy telefon / emulator USB/Wi-Fi orqali ulanmagan (`adb devices` bo'sh) | **BLOCKED** (Jismoniy qurilma yo'q) |

---

## 1C. Qaytarish (Returns) va Qaytarishni Bekor Qilish

| Test ID | Komanda / Qadam | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|
| **RET-01** | `dotnet run --project tests/Business.CoreTests -- --returns` | Qaytarish hisobi, brak/yaroqli, idempotency, takroriy bosish himoyasi, D13-D16 | ReturnAccountingTests, ReturnStoreTests, DebtReturnAndReversalTests — PASS | **PASS** |
| **RET-02** | `python tests/check_return_backup_invariants.py "<desktop_db>" "<mobile_db>"` | Asl zaxiralardagi 3 ta desktop va 2 ta mobil qaytarish invarianti | SMARTKassa 3 ta va LinePOS 2 ta qaytarishning summa, tannarx, chek, kassa va manba qatorlari 100% mos | **PASS** |
| **RET-UI** | Mobil va Desktopda interaktiv qaytarish oynalari va kassa jarayoni | Jonli oynalarda qisman, brak, reversal va xato summalarni qo'lda kiritish | Jismoniy telefon ulanmagan; avtomatik biznes yadrosi RET-01 da to'liq tasdiqlandi | **BLOCKED** (Jismoniy qurilma yo'q) |

---

## 2. Qolgan 3-bosqich — Nasiya Foydasini Kelgan Pulga Mutanosib Tan Olish (Recognised Profit)

| Test ID | Komanda / Qadam | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|
| **PRF-01** | `DebtStage1AccountingTests` | Boshlang'ich to'lov, foiz, tarixiy kurs, yaxlitlash | PASS | **PASS** |
| **PRF-02** | `DebtReportTests` (D17-D20, D26) | Nasiya paytida foyda tan olinmasligi; to'lov kuni pul ulushiga qarab tan olinishi; Day 1 da 20k to'lovga 8k foyda (`ProfitOnly=true`); D26 printer idempotency | Barcha D17-D20 va D26 testlari to'liq PASS | **PASS** |
| **PRF-03** | `DebtBackupRestoreTests` (D21-D23) | Eksport, tiklash, korruptsiya himoyasi | ALL QISM 8 BACKUP & RESTORE TESTS PASSED SUCCESSFULLY | **PASS** |
| **PRF-04** | `DebtMultiDeviceTests` | 3-qurilma topologiyasi, oflayn ortiqcha to'lov konvergentsiyasi, bloklangan mijoz to'lovi | PASS multi-device convergence | **PASS** |
| **PRF-05** | `DebtDesktopIntegrationTests` | Kassa hold/open, to'lov preview, hisob-kitoblar | PASS desktop debt integration | **PASS** |
| **PRF-MAN** | 22 ta manual ssenariy (Ekran va Excel tekshiruvi) | Desktop/mobil ekranda har bir to'lovda foyda o'zgarishi va Excel eksport | Backend hisoblash yadrosi PRF-02 da tekshirildi; UI va Excel faylini ko'rish jismoniy telefon/ekran sinovini talab qiladi | **BLOCKED** (Manual UI sinovi) |

---

## 3. Qolgan 4-bosqich — Wi-Fi LAN Sinxronizatsiya va Yakuniy Regressiya

| Test ID | Komanda / Qadam | Kutilgan | Haqiqiy | Natija |
|---|---|---|---|---|
| **LAN-01** | `dotnet run --project tests/WifiSync.CoreTests` (20 ta test) | LAN server start, pair talab qilinishi, eski protokol rad etilishi, token revoke, journal cursor, negative stock, dependency drain | 20 ta test to'liq PASS | **PASS** |
| **LAN-02** | `python tests/test_debt_schema.py` & `python tests/test_debt_wire_fixtures.py` | Schema va fixture lar yaxlitligi | 14 tests in 0.042s — OK | **PASS** |
| **LAN-03** | Commit `d569fc2`: Moliyaviy envelope yo'qligida pull to'xtashi va tiklangach yetkazilishi | Moliyaviy envelope yo'q bo'lsa kursor oldinga ketmasligi (`cursor == null`, status != 200), envelope tiklangach payment yetkazilishi | `tests/WifiSync.CoreTests` da missing envelope va repaired envelope testlari to'liq tasdiqlandi | **PASS** |
| **DEP-06** | Commit `d569fc2`: Bloklangan mijoz yangi to'lovi idempotency va DDL xatoliklar | Bloklangan qarzdor to'lovi balansni 3000 dan 2000 ga tushirishi, qayta yuborilganda dublikat bo'lmasligi, `returns` DDL xatolari inner exception bilan saqlanishi | `DebtRepositoryTests` va `DatabaseContext.cs` da barcha invariantlar to'liq tasdiqlandi | **PASS** |
| **LAN-REAL** | Real Wi-Fi routerda IP o'zgarishi, UDP discovery va qayta ulanish (A1-A15) | Telefon avtomatik yangi IP dagi desktopni topishi | 2 ta mustaqil jismoniy qurilma va router tarmog'i kerak | **BLOCKED** (Jismoniy tarmoq sinovi) |

---

## 4. Aniqlangan va Tuzatilgan Xatoliklar (Bug Fixes)

1. **`desktop/PosElectro.Desktop/Debt/DebtProfitAccounting.cs`:**
   - **Xatolik:** `Allocate` metodi oxirida `}` yopuvchi qavs tushib qolgan (`error CS1513: } expected`).
   - **Tuzatish:** Yopuvchi jingalak qavs qo'shildi.

2. **`desktop/PosElectro.Desktop/Data/DatabaseContext.cs`:**
   - **Xatolik:** Yangi ochilgan yoki test bazalarida `returns` jadvali bo'lmagani sababli `GetCashProfitReport` chaqirilganda `SQLite Error 1: 'no such table: returns'` xatosi chiqayotgan edi.
   - **Tuzatish:** `InitializeDatabase()` metodiga `returns` va `return_items` jadvallarining DDL ta'rifi kiritildi.

3. **`desktop/PosElectro.Desktop/Services/LocalSyncServer.cs` va `WifiSyncStore.cs` (Commit `d569fc2` da mustahkamlandi):**
   - **Xatolik:** Avvalgi tuzatishda envelope topilmagan guruhlar o'tkazib yuborilishi mumkin edi; bu esa moliyaviy envelope (masalan, to'lov) yo'qolgan taqdirda ham kursorni oldinga surib yuborish xavfini tug'dirar edi.
   - **Tuzatish (Fail-Closed Pull Guard):** Faqat va faqat joriy HTTP javobidagi mijozlar ro'yxatida (`customersArray`) mavjud bo'lgan sof metadata guruhlarigina (hech qanday `debt_event` bog'lanmagan bo'lsa) o'tkaziladi. Agar moliyaviy yoki noma'lum guruh uchun envelope topilmasa, server `InvalidOperationException("Qarz paketi topilmadi; kursor o‘zgarmaydi: " + groupId)` tashlaydi va kursor aslo oldinga siljimaydi (Fail-closed).

4. **`tests/Business.CoreTests/DebtRepositoryTests.cs` va `DebtMultiDeviceTests.cs`:**
   - **Xatolik:** Commit `e0be726` talabiga ko'ra bloklangan qarzdorga yangi nasiya savdo ochish rad etilishi, ammo qarzdor o'zining mavjud qarzini to'lashi mumkinligi belgilangan edi. Testlarda esa eskirgan `Reject TakePayment` kutilmasi qolib ketgan edi.
   - **Tuzatish:** Testlar biznes qoidasiga moslashtirildi: `OpenSale` bloklangan mijoz uchun rad etiladi, to'lov esa ruxsat etiladi.

5. **`tests/Business.CoreTests/DebtReportTests.cs` (D19):**
   - **Xatolik:** Stage 3 qoidalariga ko'ra, nasiya to'langanda tan olingan foyda `ProfitOnly = true` qatori sifatida qo'shiladi. Eski test esa `detailedItems.Count == 1` deb kutgan va 2 ta qator qaytganida xato bergan edi.
   - **Tuzatish:** Savdo qatori (`!x.ProfitOnly`) 1 ta ekanligi, jami tovar miqdori 1 ta va tan olingan foyda 8 000 so'm ekanligi tekshirildi.

6. **`desktop/PosElectro.Desktop/Data/DatabaseContext.cs` (Commit `d569fc2`):**
   - **Xatolik:** `InitializeDatabase()` da `returns` va `return_items` jadvallarini yaratish paytidagi xatolar jim yutilib ketayotgan edi (`catch { }`).
   - **Tuzatish:** DDL xatolari endi yashirilmaydi, sabab inner exception bilan saqlanib, baza ochilishi xavfsiz to'xtatiladi (`throw new InvalidOperationException(..., ex)`).

7. **`tests/WifiSync.CoreTests/Program.cs` va `tests/Business.CoreTests/DebtRepositoryTests.cs` (Commit `d569fc2`):**
   - **Xatolik:** Moliyaviy envelope yo'qligida pull to'xtashi va tiklangach yetkazilishi hamda arxivlangan/bloklangan mijozning yangi to'lovi hisoblanishi bo'yicha regressiya testlari yetishmayotgan edi.
   - **Tuzatish:** `tests/WifiSync.CoreTests/Program.cs` ga missing payment envelope tekshiruvi (kursor o'zgarmasligi) va envelope tiklangach muvaffaqiyatli yetkazilish testi qo'shildi. `tests/Business.CoreTests/DebtRepositoryTests.cs` ga arxivlangan mijozning yangi to'lovi balansni 3000 dan 2000 ga kamaytirishi, replay hech qanday yangi yozuv (events, lines, sales) qo'shmasligi va yangi nasiya ochish rad etilishi tekshiruvlari kiritildi.

---

## 5. Xulosa va Tavsiya

- `d569fc2` commitidagi barcha tuzatishlar va yangi qo'shilgan regressiya testlari lokal .NET 8 muhitida to'liq sinovdan o'tkazildi va tasdiqlandi (**100% PASS**). Hech qanday test talablari yoki assertionlar yumshatilmadi:
  - `dotnet run --project tests/WifiSync.CoreTests` -> **20/20 PASS**
  - `dotnet run --project tests/Business.CoreTests` -> **Barcha 15 ta suite PASS**
  - `dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj` -> **0 Error, 0 Warning PASS**
- Haqiqiy jismoniy Android telefon va Windows PC o'rtasidagi real LAN/UI tekshiruvlari jismoniy telefon ulanmaganligi sababli **BLOCKED** sifatida qayd etildi.
- Foydalanuvchi talabiga asosan kod `main` branchga merge qilinmadi, barcha o'zgarishlar faqat `fix/debt-stage3-complete` branchida saqlandi.

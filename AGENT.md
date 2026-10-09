# Line POS (Kassa va Ombor) - Loyiha Xotirasi (AGENT.md)

Ushbu faylda loyihada amalga oshirilgan barcha o'zgarishlar, arxitektura qarorlari va keyingi qadamlar qayd etib boriladi.

## 📌 Loyiha Haqida
- **Nomi:** Line
- **Turi:** Mobil POS (Kassa va Ombor nazorati)
- **Mo'ljallangan soha:** Kichik do'konlar (aynan elektr uskunalari do'koni)
- **Dizayn tizimi:** Apple Human Interface Guidelines (HIG) - Fully Rounded, Circular Controls & SF Pro Clean Typography
- **Ishlash rejimi:** 100% Oflayn (Local SQLite / Room) + CBU Onlayn Kurs Yangilash + Firebase Cloud Fleet Management
- **Til & UI:** Kotlin + Jetpack Compose (Material 3)
- **Brend ranglari:** Line Deep Ocean Blue (`#0B6477`) & Emerald Green (`#16A34A`)

## 🛠 Texnik Stek
- **Kotlin:** 2.0.21 / Compose Compiler Gradle Plugin
- **Gradle:** Kotlin DSL (`.gradle.kts`) + Version Catalog (`gradle/libs.versions.toml`)
- **Database:** Room Database (`2.6.1`) + Coroutines/Flow + KSP (Version 5 with Safe Migrations `MIGRATION_3_4`, `MIGRATION_4_5`)
- **Firebase:** Firebase Realtime Database SDK (`firebase-bom 33.7.0`) + Google Services Plugin (`4.4.2`) for Multi-Device Remote Fleet Management
- **DI:** Dagger Hilt (`2.52`)
- **Scanning:** CameraX (`1.4.0`) + Google ML Kit (`17.3.0`) + ToneGenerator BEEP + Haptic Vibrate
- **Exchange Rates:** O'zbekiston Markaziy Banki (CBU) API + Lokal Kesh
- **Licensing & Admin Fleet:** Realtime Cloud Heartbeat + Remote Device Kill Switch + Admin Multi-Device Control Dashboard (PIN `2846`)
- **Backup & Export:** SQLite Database (.db) direct copy, Excel (.xls) styled inventory, JSON formatted backup + Android Share Sheet (Telegram, Drive, Gmail)
- **Navigation:** Jetpack Navigation Compose (`2.8.3`)

---

## 📋 Amalga oshirilgan ishlar (Progress Log)
- [x] **Status Bar & Dark/Light Mode ranglari to'liq sozlandi:**
  - **Light Mode:** Status bar foni toza Oq (`Color.White`), piktogrammalar va yozuvlar (soat, batareya, wifi) esa Qora/To'q (`isAppearanceLightStatusBars = true`).
  - **Dark Mode:** Status bar foni Qorong'u (`#0F172A`), piktogrammalar esa Oq/Yorug' (`isAppearanceLightStatusBars = false`).
- [x] **Dinamik Baza Zaxirasi (DB Backup) tugmasi (Ombor oynasi pastki chap tomonida):**
  - `DatabaseBackupExporter.kt` yaratildi (SQLite `.db` fayli, chiroyli formatlangan Excel `.xls` jadvali va `.json` nusxasi).
  - `DatabaseBackupDialog.kt` yaratildi (Bitta bosish bilan Telegram, Google Drive, Gmail yoki xotiraga yuborish).
  - `ProductsScreen.kt` da pastki chap tomonda `+` tugmasi bilan bir xil vertikal balandlikda joylashtirildi.
- [x] **Firebase Realtime Database Cloud Fleet Management:**
  - `RemoteDevice.kt` va `DeviceLicensingManager.kt` real-time cloud listener bilan yangilandi.
  - `AdminPanelDialog.kt` barcha qurilmalar ro'yxati, Online/Offline holati va masofaviy bloklash/ochish bilan to'liq jihozlandi.
- [x] **Kam qolgan tovarlar Alert UI xatoligi to'liq tuzatildi:**
  - `ProductItemCard` dagi g'alati qorong'i fon olib tashlandi, toza Apple Card foni va nozik qizil hoshiya bilan tiniq holatga keltirildi.
- [x] **Back Button (Orqaga bosish) 2 bosqichli boshqaruvi (`BackHandler`):**
  - 1-marta Back: Qidiruv maydoni tozalanadi / Kategoriyadan chiqiladi.
  - 2-marta Back: Ilovadan chiqiladi.
- [x] **Omborda tovar nomini aqlli tekshirish (Dublikatlarni oldini olish):**
  - Katta-kichik harf, bosh-oxiridagi hamda o'rtasidagi ortiqcha probellar va o'zbekcha tutuq belgilari (`'`, `’`, `‘`, `ʻ`) to'liq normallashtirilib tekshiriladi.
  - 1-qadamda "Keyingisi" bosilgandayoq xatolik qizil bilan ko'rsatiladi va 2-qadamga o'tkazilmaydi.
  - `saveProduct()` da qayta tekshiruv orqali bazaga dublikat tovar tushishi to'liq bloklandi.
- [x] **"Kam qolgan tovarlar" alohida kategoriyasi:**
  - "Barchasi" kategoriyasidan so'ng darhol 2-o'ringa joylashtirildi (`CATEGORY_LOW_STOCK`).
  - Apple HIG uslubidagi ogohlantiruvchi nishon (Alert Badge), qizil nozik kontur va Warning ikonka bilan jihozlandi.
  - Bosilganda faqat qoldig'i minimal ogohlantirishdan kam bo'lgan tovarlarni saralab ko'rsatadi.
  - Yangi tovar qo'shishda virtual kategoriya sifatida tanlanib qolmasligi ta'minlandi.
- [x] **Production Release Security & R8 Obfuscation (`android-release-security`):**
  - `local.properties` orqali `CBU_API_URL` va `ADMIN_PIN` xavfsizlandi, `BuildConfig` ga ulandi (kod ichida ochiq kalit va URL-lar yo'q qilindi).
  - `.gitignore` yaratilib, `local.properties` va build artefaktlari xavfsiz himoyalandi.
  - `isMinifyEnabled = true` (R8 kod obfuscation va keraksiz kodlarni yo'qotish) va `isShrinkResources = true` yoqildi.
  - `app/proguard-rules.pro` to'liq Room, SQLite, Firebase Realtime Database, Apache POI, ML Kit, CameraX, Vico va Compose qoidalari bilan jihozlandi.
  - `assembleRelease` orqali to'liq imzolangan release APK tayyorlandi (`app/build/outputs/apk/release/app-release.apk`).
- [x] **POS Desktop (.NET 8 WPF) va Lokal Wi-Fi P2P Sinxronizatsiya:**
  - `desktop/PosElectro.Desktop` papkasida to'liq mustaqil .NET 8 WPF kassa tizimi yaratildi.
  - **Intel Celeron Optimizatsiyasi:** `VirtualizingStackPanel` bilan 10,000 tovar ko'rsatilsa ham xotira sarfi atigi ~45–70 MB RAM; og'ir GPU shaderlarisiz silliq va tezkor interfeys.
  - **Lokal Wi-Fi P2P Sinxronizatsiya:** `TcpListener` asosidagi non-elevated (administrator huquqisiz ishlovchi) ichki server (port 8080) va avtomatik Wi-Fi QR kod generatsiyasi yaratildi.
  - **Apparat skaneri:** USB HID shtrix-kod skanerining tezkor buferini ekranning istalgan joyidan ushlab oluvchi `BarcodeScannerService` ulandi.
  - **Aqlli dublikat tekshiruvi:** Android mobil ilovasidagi aqlli nom tekshiruvi (`NormalizeProductName` va `FindDuplicateProduct`) Desktop kassa tizimiga ham to'liq tatbiq etildi.
  - **Android Integratsiyasi:** `LocalSyncManager.kt` va `WifiSyncDialog.kt` orqali mobil ilovadan kompyuterga 1 tugma bilan (yoki QR orqali) internetsiz ikki tomonlama tovarlar va savdolar sinxronizatsiyasi amalga oshirildi.
  - **Desktop Build & Publish:** `dotnet publish -c Release -r win-x64 --self-contained false` orqali sinovdan o'tkazildi va `PosElectro.Desktop.exe` ishchi holatga keltirildi.
  - **Navigatsiya va Qatlamlar Tuzatilishi (ContentControl):** Stack qilingan Grid o'rniga dinamik `ContentControl Content="{Binding CurrentView}"` va DataTemplate tizimiga o'tkazildi. Dastur endi to'g'ridan-to'g'ri Kassada ochiladi, menyu tugmalari (Kassa, Ombor, Hisobotlar, Wi-Fi) erkin va silliq almashadi, hech qanday oyna bir-birini qoplab qolmaydi.
  - **Mobil QR Skaner Integratsiyasi:** `WifiSyncDialog.kt` ga kamera orqali kompyuter ekranidagi QR kodni skanerlash tugmasi (`CameraScannerDialog`) qo'shildi. `LocalSyncManager` da avtomatik JSON va URL normallashtirish (`http://` avtomatik qo'shish) yoqildi.
  - **Windows Firewall & Cleartext Traffic:** `AndroidManifest.xml` da `android:usesCleartextTraffic="true"` yoqildi. Desktop tizimiga port 8080 ni 1 tugma bilan ochish (`OpenFirewallCommand`) va `allow_firewall.bat` skripti qo'shildi.
  - **Ombor va Kassa Real-Time Sinxronizatsiyasi:** `DatabaseContext.ProductsChanged` hodisasi yaratildi va `MainViewModel` orqali ulandi. Omborda tovar qo'shilganda yoki tahrirlanganda, Kassaga o'tish bilan ro'yxat 100% yangilanadi.
  - **Apparat Skaner va Qidiruv Enter Integratsiyasi:** Shtrix-kod skanerlanganda yoki qidiruv maydonida Enter bosilganda tovar avtomatik tarzda savatga 1 ta qo'shiladi va qidiruv maydoni keyingi skanerlash uchun tozalanadi.
  - **Savatdagi Narxni Tahrirlash (Chegirma):** Savatdagi tovar narxiga ikki marta bosilganda (Double-click) modal oyna ochilib, mana shu savat/sotuv uchun narxni tahrirlash (chegirma berish) imkoni yaratildi. Bazadagi tovarning asl narxi o'zgarmaydi.
  - **Hold Savat (Multi-Cart / Kutishga qo'yish):** "⏸️ Kutishga qo'yish" tugmasi va tepada kutishdagi savatlar tablari joriy qilindi. Bir vaqtda bir nechta mijoz savatlarini boshqarish va oson tiklash mumkin.
  - [x] **Yagona "SOTISH" Tugmasi:** Keraksiz to'lov turlari (Naqd, Karta, Nasiya) olib tashlanib, o'rniga pastki qismda yirik va qulay yashil **"🛒 SOTISH (jami summa)"** tugmasi o'rnatildi.
- [x] **Mobil va Desktop Ma'lumotlar Bazalari To'liq Birxillashtirildi (DB Schema Parity):**
  - Mobilda `AppDatabase` 6-versiyaga ko'tarildi (`MIGRATION_5_6`).
  - `ProductEntity` ga `guid`, `updated_at` va `note` ustunlari qo'shildi.
  - `SaleEntity` ga `guid` va `is_synced` ustunlari, `SaleItemEntity` ga `sale_guid`, `product_guid`, `product_name` ustunlari qo'shildi.
  - Desktop SQLite bazasiga `refunds` jadvali va indekslari qo'shildi.
- [x] **Aniq Yo'nalishli O'tkazish (Directional Transfer) va Tasdiqlash Modali:**
  - Ko'r-ko'rona 2 tomonlama aralashib ketish o'rniga aniq 2 ta yo'nalish: 📤 "Telefondan ➔ Kompyuterga" va 📥 "Kompyuterdan ➔ Telefonga" kiritildi.
  - O'tkazishdan oldin avtomatik tahlil (`/api/sync/check`): Omborda nechta tovar va cheklarda nechta savdo o'zgarishi hisoblanadi va sotuvchidan tasdiq so'raladi.
- [x] **Konfliktlarni Aniqlash va "Smart Merge" (Aqlli Birlashtirish):**
  - Agar bir vaqtda ikkala qurilmada ham yangiliklar bo'lsa, ogohlantirish oynasi chiqib, ma'lumotlar yo'qolmasligi uchun "🔄 Aqlli Birlashtirish" taklif etiladi.
- [x] **Desktop Kassa Uchun Sotuvchiga Mos Jonli Monitoring UI:**
  - Qora dasturchi konsoli o'rniga "Kassa va Mobil Aloqa Markazi" yaratildi.
  - Bog'langan telefon holati (Yashil nuqta, model va IP), 3 ta KPI taqqoslash kartalari (Kompyuter, Ulangan telefon, Oxirgi o'tkazish) va o'zbek tilidagi tushunarli harakatlar jurnali o'rnatildi.
- [x] **Foydalanuvchi E'tirozlari Asosida Ikkala Tizimni Takomillashtirish (11-Sentyabr):**
  1. **Mobilda Tovar Qo'shishda "Izoh" (Note):** `ProductViewModel.kt` ga `noteInput` va `onNoteChanged` qo'shildi; `AddEditProductDialog.kt` 1-qadamiga "Izoh / Eslatma (ixtiyoriy)" kiritish maydoni ulandi; `ProductsScreen.kt` dagi tovar kartasida agar izoh mavjud bo'lsa (📝) chiroyli ko'rsatiladigan qilindi.
  2. **Desktop Baza Zaxira Nusxasi (Backup):** `DatabaseContext.cs` ga SQLite `VACUUM INTO` asosida atomik `BackupDatabase` metodi qo'shildi. `MainWindow.xaml` yuqori paneliga va `SyncView.xaml` (Aloqa markazi) ga "💾 Baza zaxirasi" tugmalari o'rnatildi. Standart Windows `SaveFileDialog` ochiladi va saqlangach, papkani explorerda ochish taklif etiladi.
  3. **Desktop Klaviatura Tili (Faqat ENG & Ruscha "Ф" -> "A"):** `KeyboardLayoutHelper.cs` yaratildi (`ActivateKeyboardLayout` va Win32 `LoadKeyboardLayout("00000409", 1)`). `MainWindow` faollashganda klaviatura avtomatik `en-US` ga o'tadi. Shuningdek, `Window_PreviewTextInput`, `CashierViewModel.HandleSearchQueryEnter` va tovarlar qidiruvida ruscha harflar ('Ф' -> 'A', 'Ы' -> 'S', ...) avtomatik inglizcha ekvivalentiga o'girilib tovarlar topilishi kafolatlandi.
  4. **Kategoriyani Qidirish, Yangi Kategoriya Qo'shish va O'lchov Birligi Birxilligi:** Desktop omborida `[➕ Yangi kategoriya]` tugmasi qo'shildi, yangi kategoriya darhol ro'yxatga qo'shiladi va tanlanadi; ComboBox qidirish rejimiga ulandi. O'lchov birligidan keraksiz `kg` olib tashlanib, mobil bilan 100% bir xil qilib faqat `dona (sht)` va `metr (m)` qoldirildi.
  5. **So'm va Dollar Tugmalarini Katta va Aniq Qilish:** `InventoryViewModel.cs` dagi valyuta tanlash `FormIsUzs` va `FormIsUsd` ga bog'landi, default holatda har doim SO'M tanlangan bo'ladi. Kichik radio tugmalar o'rniga yirik, zamonaviy `CurrencySegmentRadio` (Pill Button Toggle) yaratildi (`🇺🇿 SO'M` va `💵 USD ($)`).
  - Android Release APK (`POS_Line_Release.apk`) va Desktop Release to'liq yangilandi.
- [x] **Uzluksiz (Real-Time) Wi-Fi Jonli Sinxronizatsiya & Poyga Holati / Manfiy Qoldiq Nazorati (17-Sentyabr):**
  - **Doimiy SSE Oqimi (Server-Sent Events):** Desktop `LocalSyncServer` da `GET /api/sync/events` orqali har bir ulangan mobil mijoz uchun persistent event-stream o'rnatildi.
  - **Hodisaga asoslangan Delta Sinxronizatsiya:**
    - Desktopda savdo bo'lganda yoki tovar o'zgarganda, barcha ulangan telefonlarga 100 ms ichida `sale_created` yoki `product_updated` xabari tarqatiladi.
    - Mobilda chek urilganda yoki tovar tahrirlanganda, foniy `sendLiveSale` va `sendLiveProduct` orqali Desktop `POST /api/sync/live_sale` va `POST /api/sync/live_product` ga uzatiladi.
  - **Qoldiq Hisob-kitobi (Delta Usuli) va Poyga Holati (Race Condition / Overselling):**
    - Sinxronizatsiyada DB dagi qoldiq sonini ko'r-ko'rona ustiga yozish o'rniga, har bir chek bo'yicha `stock_quantity = stock_quantity - @quantity` amalga oshiriladi.
    - Agar 1 dona qolgan tovar bir vaqtning o'zida ikkala qurilmadan sotilsa, ikkala savdo ham saqlanadi va omborda haqiqiy qoldiq `-1` bo'lib qoladi.
    - Desktop `InventoryView` va mobil `ProductsScreen` da manfiy qoldiqlar qizil ogohlantirish belgisi bilan ko'rsatiladi: `⚠️ -1 ta (Ortiqcha sotuv)`.
  - **Oflayn Navbat va Avtomatik Qayta Bog'lanish (Offline-First Catch-up):**
    - Wi-Fi uzilib qolsa qurilmalar mustaqil ishlaydi (`is_synced = 0`). Qayta ulanganda `POST /api/sync/push_unsynced` va `GET /api/sync/delta` orqali barcha qoldiqlar 1 soniyada to'liq tenglashtiriladi.
  - **Jonli Indikatorlar:** Desktop navigatsiya panelida va mobil TopAppBar da zamonaviy jonli status tabletkalari o'rnatildi (`🟢 1 ta mobil` / `🟢 Jonli`).
  - **Production Release:** Android release APK (`POS_Line_Release.apk`, `Line_kassa_mobil.apk`) va Desktop Release publish muvaffaqiyatli yangilandi.
- [x] **Sinxronizatsiya UI Thread Xatoligi (CollectionView) va Terminal Qatorga Tushish Tuzatmasi (19-Sentyabr):**
  - **Thread Xatoligi (CollectionView Exception):** Telefondan tovar yoki savdo kelganda `DatabaseContext` hodisalari orqa fondagi HTTP threadida ishga tushib, `InventoryViewModel` va `ReportsViewModel` dagi `ObservableCollection` larni no-UI threadida o'zgartirishi oqibatida `This type of CollectionView does not support changes to its SourceCollection from a thread different from the Dispatcher thread` xatosi chiqayotgan edi. `DatabaseContext.RaiseProductsChanged()` va `RaiseWarehousesChanged()` da avtomatik `Dispatcher.Invoke` kiritildi va barcha ViewModel obunalari UI threadiga xavfsizlandi.
  - **Terminalda Qatorga Tushish (No Horizontal Scroll):** `SyncView.xaml` dagi terminal `ListBox` ga `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` va `HorizontalContentAlignment="Stretch"` o'rnatildi. Uzun matnlar yonga surilmasdan yoki kesilib qolmasdan bitta ekranda pastki qatorga chiroyli tushishi (`TextWrapping="Wrap"`) ta'minlandi.
  - **Desktop Release:** `Desktop_Release` va `publish` to'liq muvaffaqiyatli yangilandi.

- [x] **Xavfsiz Baza Eksporti & Qayta Tiklash (Safe Export & Restore) hamda Desktop Silent Auto-Backup (24-Sentyabr):**
  - **WAL Checkpoint & VACUUM INTO:** `DatabaseBackupExporter.kt` da SQLite WAL xavfi bartaraf etildi. Eksportdan oldin `VACUUM INTO` yoki `PRAGMA wal_checkpoint(TRUNCATE)` orqali barcha yangi savdolar va tovarlar 100% to'liq va butun holatda `.db` fayliga yozilib, Telegram/Driveda ulashiladi.
  - **Yangi Telefonda Bazani Tiklash (Safe Restore):** `restoreDatabaseFromUri` yaratildi. Fayl SQLite 3 standarti va `products` jadvali bo'yicha to'liq tekshiriladi, joriy Room bazasi xavfsiz yopilib, eski WAL/SHM fayllari tozalangan holda yangi baza joylashtiriladi va ilova avtomatik toza restart qilinadi.
  - **Desktop Silent Auto-Backup:** `DatabaseContext.AutoBackup` joriy qilindi. Telefondan Desktopga har safar sinxronizatsiya (`phone_to_desktop`, `smart_merge`) kelganda, kompyuter AppData dagi `Backups/` papkasiga oxirgi 15 ta xavfsiz SQLite snapshotini avtomatik saqlab boradi.
  - **Kompyuterdan Wi-Fi Orqali Tiklash:** Desktop serveriga `GET /api/sync/download_db` qo'shildi; mobilda `restoreDatabaseFromDesktop` orqali yangi telefon 1 tugma bilan kassa kompyuteridagi to'liq bazani Wi-Fi orqali tortib oladi.
  - **JSON Tovar Importi:** `importInventoryJson` orqali JSON fayldan tovarlar va ombor qoldiqlarini mavjud savdolarni o'chirmasdan import qilish imkoniyati berildi.
  - **DatabaseBackupDialog UI:** Apple HIG uslubidagi 2 ta tab ("📤 Zaxira olish" va "📥 Qayta tiklash"), xavfsizlik ogohlantirishlari, tasdiqlash dialoglari va progress ko'rsatkichlari bilan to'liq yangilandi.

- [x] **Desktop UI Takomillashtirishlari, Printer Tanlash va Jonli Preview (24-Sentyabr):**
  - **To'lov Modali & Printer Dropdown:** Eskirgan checkbox o'rniga zamonaviy ComboBox o'rnatildi (`🚫 Chek chiqarilmasin` default, `🧾 Xprinter 58mm`, `📄 A4 Printer`).
  - **Savat Boshqaruvi:** Sarlavha ustidagi noo'rin chekboks olib tashlandi; 3 ta tugma (narx turi, hold/pauza, tozalash) zamonaviy vektor SVG piktogrammalarga almashtirildi.
  - **Sinxronizatsiya (Aloqa markazi):** Qora telefon belgisi o'rniga zamonaviy yashil smarfon SVG ikonkasi o'rnatildi.
  - **Ombor Jadvali (Amallar ustuni):** Tugmalar qirqilib qolishi tuzatildi, shrift emojilari o'rniga toza vektor qalam va zamonaviy qizil savat SVG ikonkalariga o'tkazildi.
  - **Sotuv Tafsilotlari & Jonli Preview Dialog:** `SaleDetailDialog` da printerni tanlash va `ReceiptPreviewDialog` orqali 58mm termal kassa lentalari yoki rasmiy A4 hisob-faktura formatini chop etishdan oldin jonli ko'rish va chop etish imkoniyati yaratildi.
- [x] **Desktop Dasturidan Foydalanish Bo'yicha Rasmiy Minimalist Qo'llanma (29-Sentyabr):**
  - Foydalanuvchi talabiga asosan ko'zni toliqtirmaydigan, yorqin gradient va bezaksiz, rasmiy platformalar (GitHub Docs / Stripe Docs) andozasidagi texnik foydalanish qo'llanmasi ishlab chiqildi.
  - HTML shakli: `PROD/Instruksiya/LinePOS_Desktop_Foydalanish_Qollanmasi.html` (Mundarijali, sticky navigatsiyali, kbd klaviatura teglari, minimalist jadvallar va `@media print` orqali to'g'ridan-to'g'ri PDF/printerga chiqarishga mos).
  - Markdown shakli: `PROD/LinePOS_Desktop_Qollanma.md` va loyiha ildizidagi `DESKTOP_QOLLANMA.md`.
  - Qamrab olingan mavzular: Kassa (skaner, qidiruv, 1-narx/2-narx, narx tahrirlash, hold/multi-cart, to'lov turlari), Ombor (transfer, kategoriyalar, dublikat filtri, kam qolgan tovarlar, Excel eksport), Printerlar va Etiketka (Xprinter XP-365B, stiker o'lchamlari, millimetrik siljitish), Hisobotlar (KPI, moliya, cheklar tarixi), Wi-Fi Sinxron (internetsiz QR ulanish, SSE jonli sinxron, firewall), Zaxiralash (qo'lda va silent auto-backup) va Tezkor tugmalar (Hotkeys).

---


## 🔒 Muhim qoidalar
1. Kod o'zgartirishlarida mavjud ishlayotgan qismlarga zarar yetkazmaslik.
2. Har bir qadam to'liq, production-ready va toza arxitekturada bo'lishi.
3. **PROD Papkasi Standarti:** Har safar yangi release tayyorlanganda, eng so'nggi mobil APK va desktop relizlari har doim root papkadagi `PROD/` papkasiga joylashtirilishi shart (`PROD/LineKassa_Mobile_Release.apk` va `PROD/LinePOS_Desktop/`).


## Qarz daftari — 2026-10-05, 2A checkpoint

`feature/customer-debt` branchida sof C#/Kotlin hisoblash yadrosi va 97 ta umumiy fixture qo‘shildi; lokal kompilyatsiya/test/paritet o‘tdi. Baza/sync/UIga ulanmagan, main’ga merge qilinmagan. Davom ettirish: `docs/DEBT_CHECKPOINT_UZ.md`; keyingi scope faqat 2B migratsiya va transactional repository. To‘liq funksiya yoki D01–D27 integratsiya testlari tayyor deb hisoblamang.

## Qarz daftari — 2026-10-05, 2B-1

Umumiy debt schema v1, Android Room 13→14 va desktop pre-migration snapshot qo‘shildi. Kod `feature/customer-debt`da; main’ga merge yo‘q. Sxema tafsilotlari: `docs/DEBT_SCHEMA.md`. 2B hajmi sabab 2B-1 (sxema/migratsiya) va 2B-2 (transactional repository)ga bo‘lindi. Yakuniy test dalili va keyingi scope `docs/DEBT_CHECKPOINT_UZ.md`da; UI/sync/repository hali amalga oshirilmagan.

## Qarz daftari — 2026-10-07, 2B-2

C#/Kotlin local transactional repository: customer creation, nasiya ochish, payment allocation, durable request replay va HELD outbox. Savdo callbacki bir SQLite tranzaksiyasida ishlashi shart; request o‘zgarsa rad etiladi, parallel bir xil to‘lov bir marta yoziladi. Host actor/store/permission va cashier adapter hali UIga ulanmagan. `acked=-1` eski full-pull yo‘llarini to‘liq bloklamaydi: stage3 capability/atomic group tugamasdan UI/cashierga ulash yoki release qilish mumkin emas. API chegaralari `docs/DEBT_REPOSITORY.md`, yakuniy CI dalili va keyingi kichik scope `docs/DEBT_CHECKPOINT_UZ.md`da. Main’ga merge yo‘q; Firebase/CBU o‘zgarmadi.


## Qarz daftari — 2026-10-07, 3A-1

C#/Kotlin canonical wire component codec va validator qo‘shildi: customer-create, sale_open, payment; aniq integer pul, frozen allocation, payload/header mosligi va to‘liq komponent fingerprint. 92 ta bir xil fixture C#/JVM/Android uchun; mavjud 97 ta arifmetika saqlangan. Bu full envelope/DB receiver/ACK emas, `debtLedgerV1` hali ilovada e’lon qilinmaydi. HELD navbat ochilmagan; main/UI/release yo‘q. Transport auditida full pull va sync_meta’ni strip qiladigan desktop download_db yo‘li alohida integration gate deb qayd etildi. Kontrakt: `docs/DEBT_WIRE.md`; test dalili va keyingi kichik bosqich: `docs/DEBT_CHECKPOINT_UZ.md`.


## Qarz daftari — 2026-10-08, 3A-2a

C#/Kotlin `DebtSyncStore`: frozen component export, full-body replay va atomic DB import; haqiqiy bazalarda offline to‘lovlar birlashishi, qayta yuborish, ownership/permission va rollback testlari. To‘liq body+hash seali sync_meta’da saqlanadi, HELD navbat ochilmagan. Bu to‘liq sale/stock envelope, durable inbox yoki network ACK emas; trusted callbackni tarmoq bodyga to‘g‘ridan-to‘g‘ri ulash mumkin emas. Scope 3A-2a DB component, 2b full envelope/inbox, 2c transport/ACKga ajratildi. `docs/DEBT_DB_BRIDGE.md` va `docs/DEBT_CHECKPOINT_UZ.md` keyingi ish uchun asos; main/UI/release yo‘q.

## Qarz daftari — 2026-10-08, 3A-2b-1

`DebtEnvelope.cs/kt`: pure frozen financial envelope codec va sale/items/stock/FX/fee validation, 155 umumiy fixture. 3A-2b ikkiga bo‘lindi: 2b-1 codec, 2b-2 local freeze/preflight + atomic full-envelope DB receiver/inbox. Bu bosqich DB/network/UIga ulanmaydi, HELD/navbat ochilmaydi, main/release yo‘q. Keyingi ish uchun `docs/DEBT_ENVELOPE.md` va yakuniy CI dalillari bilan `docs/DEBT_CHECKPOINT_UZ.md`ni o‘qi. Local SaleFingerprint endi canonical sale wire hashiga bog‘lanishi shart; arbitrary old test fingerprintni production envelopega aylantirma.

## Qarz daftari — 2026-10-08, 3A-2b-2a

`DebtEnvelopeInbox.cs/kt`: durable validated v1 inbox, customer/payment + full receipt + pending removal bir writer tranzaksiyada. 128 packet/32MiB pending cap, exact replay va original relay; sale_open faqat WaitingForSaleAdapter, yangi public callback orqali bypass yo‘q. 2b-2b concrete sale/items/stock adapter va local freeze/preflight hali qolgan. Unknown version quarantine ham transport bosqichida; waiting hech qachon moliyaviy ACK emas. UI/main/release/Firebase/CBU yo‘q. Kontrakt `docs/DEBT_INBOX.md`, CI va keyingi scope `docs/DEBT_CHECKPOINT_UZ.md`da.


## Qarz daftari — 2026-10-09, desktop sale receiver

`DebtSaleReceiver.cs` existing inbox writer tranzaksiyasida frozen sale/items/stock/customer/account/event/full receiptni atomik saqlaydi. Optional trusted actor/user resolver desktop openingni yoqadi; default va Android gate hali saqlangan. Desktop users jadvali yo‘q, host attribution mapping beradi; Android port haqiqiy usersni ham tekshirishi kerak. Exact REAL preflight, original movement GUIDli HELD journal, full historical replay validation bor. Source freeze/local preflight va transport/main/UI/release hali yo‘q. Detallar `docs/DEBT_SALE_RECEIVER.md`, CI dalili/keyingi scope `docs/DEBT_CHECKPOINT_UZ.md`da. Firebase/CBU o‘zgarmadi.


## Qarz daftari — 2026-10-09, Android sale receiver

`DebtSaleReceiver.kt` desktop frozen sale/stock receiverini Room writer transactioniga ko‘chiradi. Mapped user haqiqiy local users jadvalida bo‘lishi shart; missing dependency qisman yozilmaydi. API26ga mos SELECT + INSERT/UPDATE, original movement GUIDli HELD markerlar va exact body/replay tekshiruvi saqlanadi. Native Room/report, 10 write-boundary rollback va >2MiB/1000-item applied receipt restart testlari qo‘shildi. CI dalili/keyingi source local freeze bosqichi `docs/DEBT_CHECKPOINT_UZ.md`da. Default gate saqlangan; UI/transport/main/release/Firebase/CBUga tegilmadi.

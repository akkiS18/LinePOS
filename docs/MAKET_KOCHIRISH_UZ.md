# SMART kassa maketini desktop kodga ko'chirish — progress

**Maket:** https://claude.ai/artifact/2yXLhCCGrta5kSoMmyzk3z ("SMART kassa — Kassa maketi", 53 ta ekran, 1366×768)
**Kod:** `desktop/PosElectro.Desktop` (WPF), branch `feature/desktop-redesign`

## O'zgarishlarni ekranda ko'rish

- `Yangi_Dizaynni_Korish.bat` (loyiha ildizida): dasturni koddan yig'adi va **alohida test bazasi** bilan ochadi
  (`%LOCALAPPDATA%\PosElectro_Dizayn\pos_desktop.db`, birinchi ochilishda ishchi bazadan nusxa olinadi). Ishchi bazaga tegmaydi.
  Test oynasiga telefonlarni ulamang.
- `Ishga_Tushirish.bat` esa ishchi kassa uchun: `PROD\LinePOS_Desktop`dagi eski o'rnatilgan build'ni ochadi, yangi dizayn unda **ko'rinmaydi**.

## Qoida (har bir agent / sessiya uchun)

1. Ishni boshlashdan oldin shu faylni o'qing, tugatgandan keyin **shu commit ichida** yangilang:
   jadvaldagi holatni, "Oxirgi qadam" bo'limini va topilgan kamchiliklarni.
2. Holat belgilari:
   - ⬜ boshlanmagan / tekshirilmagan
   - 🟡 qisman (nima qolgani izohda)
   - ✅ maketga mos (kim/qachon ekranda tekshirgani izohda)
   - ⏭ keyingi bosqich (maketda "Keyin" deb belgilangan)
3. "✅" faqat ilova ekranda ochib, maket bilan solishtirilgandan keyin qo'yiladi. Build o'tishi yetarli emas.
4. Ranglar faqat `{DynamicResource <token>}` orqali (`Themes/Theme.Light.xaml`, `Themes/Theme.Dark.xaml`).
   Yangi token qo'shilsa, ikkala faylga ham qo'shiladi (kalitlar bir xil bo'lishi shart).
   Kod ichida (C#) rang kerak bo'lsa: `SetResourceReference(..., "<token>")`.
5. "Maketni ko'chirish" = ekranning **tuzilishini** (joylashuv, komponentlar, o'lchamlar, ikonkalar) maketdagidek qayta qurish.
   Eski ekranni maket ranglariga bo'yash progress hisoblanmaydi.
6. Vaqtinchalik tema qoidasi: `App.xaml` qorong'i temani yuklaydi. Qayta qurilgan ekran o'z `Resources`iga
   `Theme.Light.xaml`ni qo'shadi (namuna: `CashierView.xaml` boshi, `MainWindow.xaml` yuqori panel).
   Hamma ekran qayta qurilgach, `App.xaml` yorug' temaga o'tkaziladi va bu qo'shimchalar olib tashlanadi.
7. Ikonkalar: maketdagi SVG yo'llari 24×24 koordinatada, `Viewbox` + `Path Style="{StaticResource Ic}"` (Stretch=None) bilan.
   SVG `rect`/`circle` → yo'lga aylantiriladi (`a` yoy buyrug'i bilan).
8. Ekran rasmini olish (ilovani ochmasdan): `scratchpad`dagi `Render` loyihasi kabi kichik .NET 8 dastur
   `CashierView`/`MainWindow`ni test bazasi nusxasida yuklab `RenderTargetBitmap` bilan PNG saqlaydi.
9. **Til:** hozir hamma matn o'zbekcha (lotin), inglizcha qoldiq bo'lmasin. Keyin dastur ko'p tilli bo'ladi
   (o'zbek lotin, o'zbek kirill, rus) — matnlar uchun joy qoldiring: matnli tugma/chiplarni qat'iy kenglikka
   sig'dirmang, chip qatorlari `WrapPanel`, uzun yozuvlar `TextTrimming`/`TextWrapping` bilan.
10. **Tema:** maketda faqat yorug' tema chizilgan. Keyin yorug'/qorong'i almashtirish qo'shiladi, qorong'i
   ranglarni agent maketga mos qilib o'zi tanlaydi. Shuning uchun har bir rang faqat token
   (`DynamicResource`) orqali va yangi token ikkala fayl (`Theme.Light.xaml`, `Theme.Dark.xaml`)ga qo'shiladi.

## Umumiy bosqichlar

| # | Bosqich | Holat | Izoh |
|---|---------|-------|------|
| A | Rang tokenlari, barcha ekranlar `DynamicResource`ga | ✅ | commit `51ff859` |
| B | Yorug' tema (maketning LIGHT palitrasi) | 🟡 | `Theme.Light.xaml` tayyor. Eski ekranlar ustiga qo'yilganda xatolar chiqdi → `App.xaml` qorong'iga qaytarildi, yorug' tema faqat qayta qurilgan ekranlarda (qoida 6) |
| C | Soyalar yorug' temaga moslash | ⬜ | `DropShadowEffect` hamma joyda qora, Opacity 0.5–0.9 (qorong'i tema uchun). Maketda: `0 16px 40px rgba(15,27,33,0.18)`. Rang/shaffoflikni tokenga chiqarish kerak |
| D | Emoji ikonkalarni vektor ikonkaga almashtirish | ⬜ | Masalan MainWindow: 🛡️ (aktivatsiya), 🖨️ (stiker), SyncView: 💡. Maketda hammasi chiziqli SVG |
| E | Har bir ekranning joylashuvini (layout) maketga moslash | ⬜ | Quyidagi jadval |
| F | Shrift: maketda Inter (`'Segoe UI'` zaxira) | ⬜ | Hozir tekshirilmagan |

## Ekranlar jadvali (E bosqichi)

Ranglar ustuni: A+B bosqichidan keyin hammasi tokenda, lekin yorug' temada ekranda ko'rilmagan.

| Maket | Ekran | Kodda | Joylashuv | Izoh |
|-------|-------|-------|-----------|------|
| TopBar | Yuqori panel | `MainWindow.xaml` | ✅ | Qayta qurildi: "S" logotip, chiziqli ikonkalar, Wi-Fi soni (yashil, faqat ulanishda), admin belgisi, soat, ⚙. Rasm olinib maket bilan solishtirildi; egasi ekranda ko'rmagan. "Kassa 1" nomi yo'q (dasturda kassa raqami tushunchasi yo'q) — faqat soat |
| 1.1 | Kassa (asosiy) | `Views/CashierView.xaml` | ✅ | Qayta qurildi: qidiruv (48px, halqa, "Enter — savatga"), plitka/ro'yxat tugmalari, kategoriya chiplari (soni bilan, endi filtrlaydi), plitkalar 176px/212px, savat sarlavhasi + 4 asbob, kutishdagi savat chiplari, savat qatori (2 qator, stepper 40px), bo'sh savat, to'lov paneli (xil·birlik, chegirma, Jami 30px, Naqd 56px, Karta/Aralash). Rasm olinib solishtirildi; egasi ekranda ko'rmagan |
| 1.2 | To'lov · Naqd (qaytim) | `CashierView.xaml` (to'lov oynasi) | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 920×712, chapda to'lov, o'ngda 340px chek. "Mijoz berdi" + tezkor summalar + Qaytim — faqat ko'rsatish (savdoga yozilmaydi). Chek matni endi oq qog'ozda qora (avval oq ustida oq edi) |
| 1.3 | Ro'yxat rejimi, ⚙ menyu, bo'sh savat | `CashierView.xaml`, `MainWindow.xaml` | ✅ | Hammasi qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. ⚙ menyu: maketdagi nomlar va chiziqli ikonkalar, karta solig'i foizi qatorda, "Admin bo'limi · PIN" sarlavhasi. Maketdagi "Mavzu (Yorug'/Qorong'i)" va "Tugma belgilari" qatorlari qo'yilmadi — dasturda bunday sozlama yo'q (soxta tugma bo'lib qolardi) |
| 1.4 | To'lov · Karta | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan |
| 1.5 | To'lov · Aralash | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan |
| 1.6 | To'lov · Nasiyaga yozish | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: almashtirgich, mijoz kartasi (bosh harflar, qarzi), muddati o'tgan ogohlantirish, 3 ustun, "Nasiyaga qoladi". Sana tanlagich matni ("Select a date") hali inglizcha |
| 1.7 | Nasiya · Tezkor mijoz qo'shish | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan |
| 1.8 | Savat qatori · narx, miqdor, chegirma | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: stepper, chegirma tugmalari (narx maydonini to'ldiradi), qator jami, "Savatdan olish" (bekor qilish imkoni bilan). Saqlash eski `SaveEditModal` |
| 1.9 | Shtrix-kod topilmadi | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Maketdan farq: Enter avvalgidek tovar qo'shadi (logika o'zgartirilmadi); "Nomi bo'yicha qidirish" oynani yopib qidiruvga qaytaradi |
| 1.10 | Brak sifatida chiqarish | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan |
| 1.11 | Savatni kutishga qo'yish | `CashierView.xaml` | ✅ | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: tezkor nom tugmalari nom maydonini to'ldiradi |
| 1.12 | Tovar rasmini kattalashtirish | `MainWindow.xaml` (PreviewImage) | ✅ | Qayta qurildi, rasm olinib solishtirildi: 640px karta, 440px rasm maydoni, nomi/qoldiq/shtrix-kod, narx, "Savatga +1" (kassadan ochilganda; mavjud savatga qo'shish buyrug'i). Oldingi/keyingi strelkalar va "1 / 3" yo'q — tovarda bitta rasm bor. Ombordan ochilganda faqat nomi |
| 2.1 | Omborlar va kategoriyalar | `Views/InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Omborlar va tanlangan omborning kategoriyalari bitta sahifada (eski "omborlar → kategoriyalar" alohida ekranlari birlashdi, logika o'zgarmadi: ombor tanlanmasa "Barcha omborlar" kategoriyalari). "Kam qolganlar N" — asboblar panelidagi tugma. Kategoriya ikonkasi nomiga qarab tanlanadi. Asosiy bo'lmagan omborda ★ (asosiy qilish) va o'chirish ikonkalari |
| 2.2 | Tovarlar jadvali | `InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: yo'l (Omborlar › ombor › kategoriya), Hammasi/Kam qolgan/Tugagan filtri (yangi, standart "Hammasi"), qatorda ⋯ menyu (kirim, rasm, o'chirish). Maketdan farq: "Excel" tugmasi, ko'p tanlovdagi "Stiker chop etish"/"Boshqa omborga" yo'q — dasturda bunday amal yo'q. Belgilash faqat "Kam qolgan tovarlar"da (buyurtma ro'yxati uchun, avvalgidek). Eski "kategoriya bo'yicha filtr" ochiluvchi ro'yxati olib tashlandi: ViewModel'da unga mos xususiyat yo'q edi (ishlamas edi) |
| 2.3 | Yangi tovar qo'shish | `InventoryView.xaml` (2-qism) | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: ikki karta (Asosiy ma'lumotlar / Narx va qoldiq), pastki panel. Haqiqiy forma `InventoryView` ichida; `AddEditProductDialog.xaml` hech qayerdan ochilmaydi (eski, tegilmadi). Qo'shildi: "Ustama +N%" (faqat ko'rsatish), Ctrl+S = Saqlash (Enter ham ishlaydi). Kategoriyani tahrirlash (✎) tugmasi saqlandi. Maketdan farq: "Saqlash va yana qo'shish" yo'q — dasturda bunday amal yo'q |
| 2.4 | Tovarni o'chirish (tasdiq) | `InventoryView.xaml` | ⬜ | |
| 2.5 | Qoldiq qo'shish (kirim) | `Views/QuickStockAddDialog.xaml` | ⬜ | |
| 2.6 | Stiker chop etish sozlamalari | `Views/BarcodePrintDialog.xaml` | ⬜ | |
| 2.7 | Stiker · printer va kalibrovka | `BarcodePrintDialog.xaml` | ⬜ | |
| 2.8 | Stiker chop etilmoqda (oyna) | `MainWindow.xaml` | ⬜ | |
| 2.9 | Stiker fonda davom etyapti (vidjet) | `MainWindow.xaml` | ⬜ | |
| 2.10 | Buyurtma ro'yxati (kam qolganlar) | `Views/ReorderListDialog.xaml` | ⬜ | |
| 2.11 | Omborlararo ko'chirish | `InventoryView.xaml` | ⬜ | |
| 2.12 | Yangi ombor | `InventoryView.xaml` | ⬜ | |
| 2.13 | Kategoriya qo'shish / tahrirlash | `InventoryView.xaml` | ⬜ | |
| 3.1 | Mijozlar va nasiya cheklari | `Views/DebtsView.xaml` | ⬜ | |
| 3.2 | Qarz to'lovini qabul qilish | `DebtsView.xaml` | ⬜ | |
| 3.3 | To'lovlar tarixi | `DebtsView.xaml` | ⬜ | |
| 3.4 | Yangi mijoz / tahrirlash | `DebtsView.xaml` | ⬜ | |
| 3.5 | Ortiqcha pulni qaytarish | `DebtsView.xaml` | ⬜ | |
| 3.6 | Mijoz haqida ma'lumot | `DebtsView.xaml` | ⬜ | |
| 3.7 | Mijozni bloklash (tasdiq) | `DebtsView.xaml` | ⬜ | |
| 3.8 | Mijoz ko'chirmasi (chop etish) | `DebtsView.xaml` | ⬜ | |
| 4.1 | Hisobotlar · umumiy ko'rinish | `Views/ReportsView.xaml` | ⬜ | |
| 4.2 | Chek tafsiloti | `Views/SaleDetailDialog.xaml` | ⬜ | |
| 4.3 | Admin PIN so'rovi | `Views/AdminPinDialogs.cs` | ⬜ | |
| 4.4 | Sana oralig'ini tanlash | `ReportsView.xaml` | ⬜ | |
| 4.5 | Tovarni qaytarish | `Views/ReturnDialog.cs` | ⬜ | Eski kalitlar (`BgDark`, `TextPrimary`...) shu yerdan o'qiladi |
| 4.6 | Chekni oldindan ko'rish (58 mm / A4) | `Views/ReceiptPreviewDialog.xaml` | ⬜ | Chek qog'ozi ataylab oq |
| 4.7 | Qaytarishni bekor qilish | `Views/ReturnReversalDialog.cs` | ⬜ | Eski kalitlar shu yerdan ham o'qiladi |
| 5.1 | Wi-Fi Sinxron | `Views/SyncView.xaml` | ⬜ | Qadam raqamlari maketdagidek och fonga o'tkazildi |
| 5.2 | Ulanish yo'riqnomasi | `SyncView.xaml` | ⬜ | |
| 5.3 | Server to'xtatilgan, qurilma yo'q | `SyncView.xaml` | ⬜ | |
| 6.1 | Dastur yopiq — aktivatsiya | `MainWindow.xaml` | ⬜ | |
| 6.2 | Aktivatsiya paroli (xato holati) | `MainWindow.xaml` | ⬜ | |
| 6.3 | ⚙ Karta solig'i | `MainWindow.xaml` | ⬜ | |
| 6.4 | ⚙ Bazani zaxiradan tiklash | `Views/RestoreConfirmDialog.cs` | ⬜ | |
| 6.5 | ⚙ Dasturchi bilan bog'lanish | `MainWindow.xaml` | ⬜ | |
| 6.6 | ⚙ Zaxira nusxa saqlandi | `MainWindow.xaml` | ⬜ | |
| 6.7 | ⚙ Admin PIN · yashirin ma'lumotlar | `Views/AdminPinDialogs.cs` | ⬜ | |
| Keyin | Kassa qorong'i tema | `Themes/Theme.Dark.xaml` | ⏭ | Tokenlar tayyor; tema almashtirish tugmasi yo'q |
| Keyin | Kassa 1920×1080 | — | ⏭ | |

## Oxirgi qadam

**2026-11-13 (Claude Code), 3-qadam — Kassa modal oynalari qayta qurildi:**
- 1.2/1.4/1.5/1.6 To'lov, 1.7 Tezkor mijoz, 1.8 Narx/miqdor, 1.9 Shtrix-kod topilmadi, 1.10 Brak, 1.11 Kutish va bildirishnoma (pastda markazda; "Bekor qilish" amali bo'lsa to'q fon) maket tuzilishida yozildi.
- Logika o'zgarmadi: barcha buyruqlar, nomlar (`PaymentConfirmBtn`, `TxtHoldCartName` va h.k.), klaviatura ishlovchilari, Esc/Enter avvalgidek. ViewModel'ga faqat ko'rsatish uchun yordamchilar qo'shildi (`QuickCashOptions`, `ChangeAmount`, `EditLineTotal`, `EditActiveDiscount`, `SetHoldNameCommand`, `ConfirmSaleLabel`, `DebtCustomerItemDto.Initials`). Chegirma/miqdor tugmalari faqat maydonlarni to'ldiradi.
- Yangi: `Views/EqualsConverter.cs`; tokenlar `PaperBg/PaperInk/PaperRule`, `ToastBg/ToastFg/ToastBtnBg/ToastBtnFg`.
- Build o'tdi, 37 test o'tdi. Har bir oyna test bazasi nusxasida rasmga olinib maket bilan solishtirildi.
- Keyin: 1.12 tovar rasmi oynasi va ⚙ menyu qatorlari (`MainWindow.xaml`) maketga keltirildi. `MainViewModel.ShowImage` ga qo'shimcha variant (meta, narx, "Savatga +1"); eski chaqiruv o'zgarmagan.
- Egasi 1.x ni ko'rib chiqdi: "qolgani hammasini to'g'ri qilgansan". Tuzatildi: sana tanlagichdagi inglizcha "Select a date" → "Sanani tanlang" (`App.xaml`, butun dastur), oy nomlari o'zbekcha; Kassa oynalaridagi chip qatorlari uzun matn uchun o'raladigan (qoida 9).

**2026-11-13 (Claude Code), 4-qadam — Ombor asosiy ekrani (2.1, 2.2):**
- `InventoryView.xaml` 1-qismi maket tuzilishida qayta yozildi (yorug' tema faqat shu qismda; tovar formasi va oynalar hali eski, qorong'i).
- Umumiy uslublar yangi faylda: `Themes/SmartControls.xaml` (tugma, maydon, chip, ikonka...). `CashierView` hozircha o'z nusxasini saqlaydi — keyin shu faylga o'tkazish kerak.
- ViewModel'ga faqat ko'rsatish uchun: `GridCategoryCards`, `LowStockCard`, `CategoryCardModel.LowCount/OutCount/IconData/BadgeText`, qidiruv yozuvlari; yangi kichik filtr `StockFilter` (kategoriya ichida, standart 0 = avvalgidek hammasi).
- Yangi: `Views/SameValueMultiConverter.cs` (tanlangan ombor kartasi).

**2026-11-13 (Claude Code), 5-qadam — Tovar formasi (2.3):**
- `InventoryView.xaml` 2-qismi maket tuzilishida qayta yozildi (yorug' tema). Barcha bog'lanishlar, skaner maydoni, Admin PIN qulfi, USD hisoblash avvalgidek.
- ViewModel: sarlavhalar "Yangi tovar" / "Tahrirlash: nom"; `FormMarkupText` (ustama foizi, faqat ko'rsatish).
- Tuzatish: `FieldBox` (SmartControls va CashierView nusxasi) ichki chekkani ikki marta qo'yardi — matn maydon ichida ~30px surilib turardi. Endi bir marta; kassadagi maydonlar ham maketdagidek.
- **Keyingi qadam:** 2.4–2.13 Ombor oynalari (o'chirish tasdig'i, kirim, stiker, buyurtma ro'yxati, ko'chirish, yangi ombor, kategoriya).

**2026-11-13 (Claude Code), 2-qadam — Kassa ekrani qayta qurildi:**
- Egasi fikri: yorug' rang eski joylashuv ustiga tortilgani noto'g'ri, maket **tuzilishi** ko'chirilishi kerak (qoida 5).
- `App.xaml` yana qorong'i tema; yorug' tema faqat `CashierView` va yuqori panelda (qoida 6).
- `CashierView.xaml` asosiy qismi (1.1) va yuqori panel (TopBar) maketdagi tuzilishda qayta yozildi. Modal oynalar (1.2, 1.4–1.11) hali eski joylashuvda, lekin endi yorug' tema ichida.
- ViewModel: `CategoryChips` (kategoriya bo'yicha filtr ishlaydi), `IsListMode`, `SubtotalAmount`/`DiscountText`/`CartItemsLine`; `Product.PriceText`/`StockPillText`/`CategoryWarehouseMeta`; `CartItemModel.PriceLineText`/`ShowOriginalPrice`; `MainViewModel.ClockText`.
- Yangi tokenlar (ikkala temada): `AccentRing`, `IllusBg`, `OnAccentChip`.
- Build o'tdi, 37 test o'tdi. Ekran rasmlari test bazasi nusxasida olinib maket bilan solishtirildi.
- **Keyingi qadam:** egasi `Yangi_Dizaynni_Korish.bat` bilan ko'radi → 1.1 va TopBar ✅. So'ng 1.2 To'lov oynasi (maketdagi 920×712, chapda to'lov turlari, o'ngda 340px chek), keyin 1.4–1.11.

## Ma'lum muammolar

- Raqamlar formati: maketda `12 000` (bo'sh joy), dasturda `12,000` (WPF binding `en-US` tilida). Butun dastur uchun bitta joyda hal qilish kerak (`FrameworkElement.Language` + `CurrentCulture`), bir ekranda emas. Sana ham shunga bog'liq: sana tanlagich o'zbekcha (`uz-Latn-UZ`: "Sanani tanlang", oy nomlari), lekin format `11/12/2026`; maketdagi `11.12.2026` shu umumiy sozlama bilan birga hal qilinadi. Ko'p tillilik (qoida 9) bilan birga qilish ma'qul.
- Oyna sarlavha qatori qorong'i (`WindowStyleHelper.ApplyDarkTheme`). App to'liq yorug' temaga o'tganda yorug'ga almashtirish kerak.

- `ReportsViewModelSearchTests.Search_lists_newest_first...` testi soat 03:00 gacha yiqiladi: "3 soat oldin" sotuv kechagi kunga tushadi. Kod xatosi emas, testni vaqtga bog'liq bo'lmaydigan qilish kerak.
- Android ilovada (`app/`) commit qilinmagan o'zgarishlar bor: nom "Elektromarket", `versionCode` 6 → 5 (yangilanish o'rnatilmaydi), test ID olib tashlangan. Maketga aloqasi yo'q, egasining qarori kutilmoqda.

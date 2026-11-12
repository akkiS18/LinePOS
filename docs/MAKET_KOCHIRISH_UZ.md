# SMART kassa maketini desktop kodga ko'chirish — progress

**Maket:** https://claude.ai/artifact/2yXLhCCGrta5kSoMmyzk3z ("SMART kassa — Kassa maketi", 53 ta ekran, 1366×768)
**Kod:** `desktop/PosElectro.Desktop` (WPF), branch `feature/desktop-redesign`

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

## Umumiy bosqichlar

| # | Bosqich | Holat | Izoh |
|---|---------|-------|------|
| A | Rang tokenlari, barcha ekranlar `DynamicResource`ga | ✅ | commit `51ff859` |
| B | Yorug' tema (maketning LIGHT palitrasi) asosiy qilindi | 🟡 | `Theme.Light.xaml`, `App.xaml` shuni yuklaydi. Build o'tdi, **ekranda ko'rilmagan** |
| C | Soyalar yorug' temaga moslash | ⬜ | `DropShadowEffect` hamma joyda qora, Opacity 0.5–0.9 (qorong'i tema uchun). Maketda: `0 16px 40px rgba(15,27,33,0.18)`. Rang/shaffoflikni tokenga chiqarish kerak |
| D | Emoji ikonkalarni vektor ikonkaga almashtirish | ⬜ | Masalan MainWindow: 🛡️ (aktivatsiya), 🖨️ (stiker), SyncView: 💡. Maketda hammasi chiziqli SVG |
| E | Har bir ekranning joylashuvini (layout) maketga moslash | ⬜ | Quyidagi jadval |
| F | Shrift: maketda Inter (`'Segoe UI'` zaxira) | ⬜ | Hozir tekshirilmagan |

## Ekranlar jadvali (E bosqichi)

Ranglar ustuni: A+B bosqichidan keyin hammasi tokenda, lekin yorug' temada ekranda ko'rilmagan.

| Maket | Ekran | Kodda | Joylashuv | Izoh |
|-------|-------|-------|-----------|------|
| TopBar | Yuqori panel | `MainWindow.xaml` | ⬜ | |
| 1.1 | Kassa (asosiy) | `Views/CashierView.xaml` | ⬜ | |
| 1.2 | To'lov · Naqd (qaytim) | `CashierView.xaml` (to'lov oynasi) | ⬜ | |
| 1.3 | Ro'yxat rejimi, ⚙ menyu, bo'sh savat | `CashierView.xaml`, `MainWindow.xaml` | ⬜ | |
| 1.4 | To'lov · Karta | `CashierView.xaml` | ⬜ | |
| 1.5 | To'lov · Aralash | `CashierView.xaml` | ⬜ | |
| 1.6 | To'lov · Nasiyaga yozish | `CashierView.xaml` | ⬜ | |
| 1.7 | Nasiya · Tezkor mijoz qo'shish | `CashierView.xaml` | ⬜ | |
| 1.8 | Savat qatori · narx, miqdor, chegirma | `CashierView.xaml` | ⬜ | |
| 1.9 | Shtrix-kod topilmadi | `CashierView.xaml` | ⬜ | |
| 1.10 | Brak sifatida chiqarish | `CashierView.xaml` | ⬜ | |
| 1.11 | Savatni kutishga qo'yish | `CashierView.xaml` | ⬜ | |
| 1.12 | Tovar rasmini kattalashtirish | `MainWindow.xaml` (PreviewImage) | ⬜ | Yopish tugmasi tokenga o'tkazildi |
| 2.1 | Omborlar va kategoriyalar | `Views/InventoryView.xaml` | ⬜ | |
| 2.2 | Tovarlar jadvali | `InventoryView.xaml` | ⬜ | |
| 2.3 | Yangi tovar qo'shish | `Views/AddEditProductDialog.xaml` | ⬜ | |
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

**2026-11-13 (Claude Code):**
- Commit `51ff859`: rang tokenlari, tovar rasmlari, admin PIN (avvalgi, commit qilinmagan ish).
- Yorug' tema: `Themes/Theme.Light.xaml` yaratildi va asosiy qilindi.
- Yorug' fonda ko'rinmay qoladigan joylar tuzatildi:
  - kulrang (`Line`/`LineStrong`) tugmalardagi oq matn va ikonkalar `Ink` qilindi;
  - ombor kartochkasidagi yulduzcha va strelka tokenga o'tkazildi;
  - savatdagi tovarlar soni belgisi tokenga o'tkazildi;
  - qarz to'lov usuli tugmalari tokenga o'tkazildi;
  - Sinxron qadam raqamlari och fonga o'tkazildi;
  - rasm oynasining yopish tugmasi tokenga o'tkazildi.
- Build o'tdi. Ilova ekranda **ochilmagan** (shu kompyuterdagi haqiqiy baza va aktivatsiya bilan ishlaydi).
- **Keyingi qadam:** ilovani ochib, yorug' temani ko'rish (B → ✅). Keyin C (soyalar), so'ng E: 1.1 Kassa ekranidan boshlab joylashuvni maketga moslash.

## Ma'lum muammolar

- `ReportsViewModelSearchTests.Search_lists_newest_first...` testi soat 03:00 gacha yiqiladi: "3 soat oldin" sotuv kechagi kunga tushadi. Kod xatosi emas, testni vaqtga bog'liq bo'lmaydigan qilish kerak.
- Android ilovada (`app/`) commit qilinmagan o'zgarishlar bor: nom "Elektromarket", `versionCode` 6 → 5 (yangilanish o'rnatilmaydi), test ID olib tashlangan. Maketga aloqasi yo'q, egasining qarori kutilmoqda.

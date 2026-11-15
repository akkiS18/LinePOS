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
| 2.4 | Tovarni o'chirish (tasdiq) | `InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Windows MessageBox o'rniga ichki oyna; o'chirish amali o'sha (yumshoq o'chirish). Qoldiq bo'lsa ogohlantiradi. Esc = bekor; Enter o'chirmaydi (xavfli amal). Maketdan farq: bir vaqtda bitta tovar (ko'p tanlab o'chirish dasturda yo'q), "10 soniya Qaytarish" yo'q — bunday amal yo'q |
| 2.5 | Qoldiq qo'shish (kirim) | `Views/QuickStockAddDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. "Hozir + Qo'shiladi = Yangi qoldiq", +10/+50/+100 tugmalari (maydondagi songa qo'shadi). Oyna asosiy oynani qoplaydi (fon xiralashadi). Kirim amali o'sha. Maketdan farq: "Yangi tan narx", "Yetkazib beruvchi / izoh", "Kirgandan keyin stiker chop etish" va "Bir o'ram (100)" yo'q — kirim amali faqat sonni qabul qiladi |
| 2.6 | Stiker chop etish sozlamalari | `Views/BarcodePrintDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 900×640 karta, chapda sozlamalar, o'ngda jonli ko'rinish (printer chiqishi bilan bir xil render), sarlavhada printer holati. Chop etish, sinov, sozlamalarni saqlash avvalgidek. Maketdan farq: bir vaqtda bitta tovar (ko'p tovarli ro'yxat dasturda yo'q), shuning uchun "Hammasiga" o'rniga "Tez tanlash" |
| 2.7 | Stiker · printer va kalibrovka | `BarcodePrintDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: ochiluvchi bo'lim — printer, stiker o'lchami, nomni surish, umumiy surish (0,5 mm qadam). Yopiq holatda qisqa yozuv (o'lcham · nom · umumiy) |
| 2.8 | Stiker chop etilmoqda (oyna) | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 440px karta — tovar nomi, `27 / 61`, jarayon chizig'i, "To'xtatish" / "Fonda davom etsin". Tugagan, to'xtatilgan va xato holatlari shu kartada. Maketdagi "Taxminan 40 soniya qoldi · printer nomi" yo'q (xizmatda bunday hisob yo'q) — o'rnida foiz |
| 2.9 | Stiker fonda davom etyapti (vidjet) | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: o'ng pastda 320px to'q vidjet (`ToastBg`), "Stiker: 27 / 61", "To'xtatish", yopish (✕) tugmasi saqlandi. Yangi tokenlar: `ToastTrack`, `ToastFill`, `ToastLine` (Light va Dark) |
| 2.10 | Buyurtma ro'yxati (kam qolganlar) | `Views/ReorderListDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 900×620 karta, jadval (rasm, tovar, shtrix-kod, qoldiq belgisi, tan narx — admin bo'lmasa `••••`), qatorda ✕ (olib tashlash), "Ro'yxatni tozalash", "Chek printerga", "Excel'ga eksport". Maketdagi chegara va kategoriya filtrlari hamda tahrirlanadigan "Buyurtma" soni yo'q — dasturda bunday logika yo'q (ro'yxat Omborda belgilangan tovarlardan tuziladi) |
| 2.11 | Omborlararo ko'chirish | `InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 560px karta, Qayerdan ⇄ Qayerga qutilari, tovar, miqdor, "140 → 100" natija qatorlari, tugmada miqdor. VM'ga faqat ko'rsatish xossalari va almashtirish (⇄) buyrug'i qo'shildi; ko'chirish logikasi o'zgarmadi. Maketdagi tovar qidiruvi o'rnida ochiluvchi ro'yxat (avvalgidek) |
| 2.12 | Yangi ombor | `InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 440px karta, nom maydoni, "Asosiy ombor qilish" kartasi (hozirgi asosiy ombor nomi bilan) |
| 2.13 | Kategoriya qo'shish / tahrirlash | `InventoryView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 480px karta, nom maydoni, tahrirlashda "N ta tovar". Maketdagi kategoriya rasmi tanlash va "O'chirish" yo'q — dasturda kategoriya rasmi ham, kategoriyani o'chirish ham mavjud emas |
| 3.1 | Mijozlar va nasiya cheklari | `Views/DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 4 ta KPI karta, chapda 380px mijozlar ro'yxati (qidiruv, "Mijoz" tugmasi, filtr belgilari, bosh harfli qatorlar), o'ngda mijoz kartasi va tablar. Filtrlar dasturdagi beshtasi (Hammasi, Qarzdor, Muddati o'tgan, Yopilgan, Haqdor) — maketdagi "Bloklangan" filtri va belgilardagi sonlar yo'q (logikasi yo'q). "Eng yaqin muddat"da "3 kun o'tdi" yo'q. Chek jadvalidagi "To'langan" ustuni "Qoplangan" deb nomlandi (qaytarishlar ham kiradi) |
| 3.2 | Qarz to'lovini qabul qilish | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 520px karta, joriy qarz, Naqd/Karta/Aralash segmenti, katta summa maydoni, taqsimot qutisi. Maketdagi "To'liq" va "Eski chekni yopish" tez tugmalari hamda tugmadagi summa yo'q (logikasi yo'q) |
| 3.3 | To'lovlar tarixi | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: jadval: sana, turi (rangli belgi), summa, usul, tafsilot, bekor qilish tugmasi. Maketdagi "Kassir" ustuni yo'q (ma'lumot saqlanmaydi); "Qaysi chekka" o'rnida mavjud tafsilot matni |
| 3.4 | Yangi mijoz / tahrirlash | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: 440px karta (maket K-YangiMijoz): ism, telefon, izoh |
| 3.5 | Ortiqcha pulni qaytarish | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: alohida maketi yo'q — to'lov oynasi uslubida qurildi (naqd / karta maydonlari) |
| 3.6 | Mijoz haqida ma'lumot | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: alohida maketi yo'q — umumiy uslubda; "Boshqa amallar" (⋯) menyusidan ochiladi |
| 3.7 | Mijozni bloklash (tasdiq) | `DebtsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan: alohida maketi yo'q — 2.4 o'chirish tasdig'i uslubida ekran ichidagi oyna (avval tizim MessageBox'i edi); bloklash amali o'zgarmadi |
| 3.8 | Mijoz ko'chirmasi (chop etish) | `DebtsView.xaml` | ⬜ | Tegilmadi: alohida maketi yo'q, ko'chirma chek printeriga matn sifatida ketadi, natija tizim xabari bilan ko'rsatiladi |
| 4.1 | Hisobotlar · umumiy ko'rinish | `Views/ReportsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Filtrlar qatori, 4 KPI kartasi (foyda PIN ortida), soatlik tushum grafigi, eng ko'p sotilgan 5 tovar, cheklar jadvali. Grafik, top-5 va karta ostidagi izohlar (`O'rtacha chek`, `N xil tovar`) `ReportsViewModel`da mavjud hisobot satrlaridan faqat ko'rsatish uchun yig'iladi — hisob-kitob o'zgarmadi. Raqam formati (`344,366`) ilova bo'yicha keyin `344 366`ga o'tkaziladi |
| 4.2 | Chek tafsiloti | `Views/SaleDetailDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 640px karta: tovarlar jadvali (tan narx faqat admin uchun), Jami, Sof foyda, qaytarishlar tarixi; tugmalar: Qaytarish, ko'rish (ko'z), A4 hisob-faktura, Chekni qayta chiqarish (Enter). Format tanlash ro'yxati o'rniga ikkita alohida tugma — chop etish amallari o'sha-o'sha. Maketdagi `Chegirma` va `Berildi / qaytim` qatorlari yo'q: chekda bu ma'lumot saqlanmaydi (aralash to'lovda `Naqd / karta` ko'rsatiladi) |
| 4.3 | Admin PIN so'rovi | `Views/AdminPinDialogs.cs` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 360px karta: qulf belgisi, 4 katak, 3×4 raqamli klaviatura (sichqoncha va klaviaturadan), Esc/⌫; 4-raqamda avtomatik tekshiradi (`AdminGate.TryUnlock` o'zgarmadi). Shu fayldagi `AdminPinSettingsDialog` (sozlamalar) hali eski ko'rinishda — 6.x bosqichida |
| 4.4 | Sana oralig'ini tanlash | `ReportsView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Tez tanlov + ikki oy yonma-yon; kun ranglari endi tokenlardan (avval `CalendarDayItem`dagi qattiq to'q ranglar edi — ular ishlatilmaydi, lekin model o'zgarmadi) |
| 4.5 | Tovarni qaytarish | `Views/ReturnDialog.cs` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 760px karta: tovar qatorlari (− son +, qaysi omborga, brak belgisi), brak izohi, sabab, naqd/karta summalari, pastda jami va tugmalar; summalarni tasdiqlash oynasi ham maket uslubida. Faqat konstruktordagi ko'rinish qismi almashtirildi — hisob-kitob, qoralama va saqlash mantig'i o'zgarmadi. Maketdan farqi: brak har bir tovar uchun alohida (bitta umumiy belgi emas), `Naqd / Karta` almashtirgichi o'rniga ikkita summa maydoni (dastur bo'lib qaytarishni qo'llaydi), sabab — erkin matn (tayyor ro'yxat yo'q), karta xarajati maydoni saqlandi |
| 4.6 | Chekni oldindan ko'rish (58 mm / A4) | `Views/ReceiptPreviewDialog.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 1000×700 karta: ikkala qog'oz yonma-yon, tanlangani hoshiyada (qog'oz ustiga bosib ham tanlanadi), 58 mm / A4 almashtirgich, Chop etish, pastda chek printeri. Qog'oz ataylab oq va yozuvi qora (har qanday temada) — shu fayldagi `Paper*` ranglari tokensiz. Maketdagi `PDF saqlash` tugmasi yo'q: bunday amal dasturda mavjud emas |
| 4.7 | Qaytarishni bekor qilish | `Views/ReturnReversalDialog.cs` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 500px karta: belgi, savol, qisqa ma'lumot (qaytarish raqami, tovarlar, summa), sabab, `Yo'q, qoldirish` / `Bekor qilish`. Bekor qilish mantig'i o'zgarmadi. Maketdagi `Ombor` va `Kassa` ta'siri qatorlari yo'q: bu ma'lumot oynaga berilmaydi. Eski kalitlar (`BgDark`, `TextPrimary`...) endi o'qilmaydi — ikkala temadan olib tashlandi. Umumiy ko'rinish yordamchisi: `Views/ModalKit.cs` |
| 5.1 | Wi-Fi Sinxron | `Views/SyncView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi (soxta ma'lumot bilan — render paytida haqiqiy server yoqilmadi); egasi ekranda ko'rmagan. Chapda 420px karta: holat belgisi, QR, manzil + `Nusxalash`, 3 qadam, `Yangi telefon uchun QR`, qo'shimcha sozlamalar (ulanish kodi, Firewall, ruxsatni bekor qilish), `Serverni to'xtatish`. O'ngda qurilmalar kartalari va jurnal (tur belgisi: SINXRON / MA'LUMOT / OGOH / XATO). Maketdan farqi: qurilma roli (`Kassir telefoni`) va `Faqat xatolar` filtri yo'q — dasturda bunday ma'lumot/amal mavjud emas; `oxirgi faollik` o'rniga ulangan vaqti |
| 5.2 | Ulanish yo'riqnomasi | `SyncView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 640px oyna: 4 qadam, sariq `Ulanmasa` izohi, `Tushunarli` (Esc/Enter). Matnlar dasturdagi haqiqiy amallarga moslandi (manzil jonli ko'rsatiladi) |
| 5.3 | Server to'xtatilgan, qurilma yo'q | `SyncView.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Uzuq chiziqli katak, `Server to'xtatilgan`, `Serverni ishga tushirish`; qurilmalar va jurnalning bo'sh holatlari. Maketdagi `Dastur ochilganda avtomatik yoqilsin` tugmachasi yo'q: bunday sozlama dasturda mavjud emas (server har doim avtomatik yoqiladi) |
| 6.1 | Dastur yopiq — aktivatsiya | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Butun oynani qoplaydigan sahifa: brend, 540px karta (qulf belgisi, model, Qurilma ID + `Nusxa`, masofaviy ruxsat holati, `Aktivatsiya paroli bilan ochish`, `Dasturchi bilan bog'lanish`). Logoga 3 marta bosish amali saqlandi |
| 6.2 | Aktivatsiya paroli (xato holati) | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 460px oyna: parol maydoni (xatoda qizil hoshiya), ko'rsatish/yashirish tugmasi, xato qatori, `Bekor` / `Ochish Enter`. Tekshirish mantig'i o'zgarmadi; faqat ikkita xato matni emojisiz qilib yozildi |
| 6.3 | ⚙ Karta solig'i | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 480px oyna: katta foiz maydoni, tayyor tugmalar (1%, 1.8%, 2%, 3% — maydonga yozadi, saqlash `Saqlash`da), `Admin rejimi ochiq` belgisi. Maketdagi izoh («narxga qo'shiladi», «1 020 000 so'm» misoli) olinmadi: dasturda bu foiz narxga qo'shilmaydi, karta tushumidan xarajat sifatida ushlanadi — matn shunga mos |
| 6.4 | ⚙ Bazani zaxiradan tiklash | `Views/RestoreConfirmDialog.cs` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 540px oyna (ModalKit): qizil chiziq, fayl ma'lumoti, nima yo'qolishi, avtomatik zaxira izohi, `TIKLASH` deb yozish, `Bekor` / `Tiklash`. Tasdiqlash sharti o'zgarmadi. Maketdagi `Boshqa fayl` tugmasi yo'q: fayl oynadan oldin tanlanadi |
| 6.5 | ⚙ Dasturchi bilan bog'lanish | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 420px oyna: Telegram, telefon (+ `Nusxa`), `Telegramda ochish`, pastda Qurilma ID. Maketdan farqi: telefon qatori saqlandi (dasturda bor edi); versiya raqami ko'rsatilmaydi |
| 6.6 | ⚙ Zaxira nusxa saqlandi | `MainWindow.xaml` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. Pastda 560px bildirishnoma: fayl yo'li va hajmi, `Papkani ochish`, ✕ (avval tizim MessageBox'i edi; zaxiralashning o'zi o'zgarmadi). Maketdagi «oxirgi zaxira N kun oldin» eslatmasi yo'q: dastur oxirgi zaxira sanasini saqlamaydi |
| 6.7 | ⚙ Admin PIN · yashirin ma'lumotlar | `Views/AdminPinDialogs.cs` | 🟡 | Qayta qurildi, rasm olinib solishtirildi; egasi ekranda ko'rmagan. 520px oyna (ModalKit): yoqish/o'chirish tugmachasi, PIN so'raladigan joylar, PIN va takrori, `Bekor` / `Saqlash`. Saqlash mantig'i o'zgarmadi. Maketdan farqi: PIN 4 ta alohida katakda emas, bitta maydonda. Eski `DialogKit` olib tashlandi |
| Keyin | Kassa qorong'i tema | `Themes/Theme.Dark.xaml` | ⏭ | Tokenlar tayyor; tema almashtirish tugmasi yo'q |
| Keyin | Kassa 1920×1080 | — | ⏭ | |

## Oxirgi qadam

**2026-11-15 (Claude Code) — Kassa: Space = Naqd to'lash; Sinxron jurnali belgisi:**
- Tugmada `Space · F8` yozilgan, lekin Space hech qachon ishlamas edi: qidiruv maydoni doim fokusda, Space esa faqat matn maydonidan tashqarida ishlardi. Endi qidiruv maydoni **bo'sh** bo'lsa Space naqd to'lovni ochadi; maydonda matn bo'lsa probel odatdagidek yoziladi (`CashierView.SpaceShouldPay`, `MainWindow.xaml.cs`). Ushlab turilgan Space takrorlanmaydi.
- Sinxron jurnali: `success` yozuvlar `SINXRON` deb belgilanar edi, aslida ular Firewall ruxsati va zaxira nusxa haqida — `BAJARILDI` ga o'zgartirildi.
- Ma'lum cheklov: jurnal telefon sinxronini jonli ko'rsatmaydi. Server faqat ishga tushganini va ishga tushish xatosini yozadi; qolgan yozuvlar — QR xatosi, Firewall, zaxira. Egasining qarori kutilmoqda (olib tashlash yoki haqiqiy voqealarni yozish).
- 4 ta yangi test (`CashierSpaceShortcutTests`), jami 50 o'tdi. Klaviatura bilan haqiqiy ekranda sinalmagan.

**2026-11-15 (Claude Code) — Hisobotlar (4.1): statistika qismi yig'iladi:**
- Egasi talabi (maketda yo'q): ko'rsatkich kartalari, soatlik grafik va "Eng ko'p sotilganlar" bitta `Statistika` sarlavhasi ostiga yig'iladi, cheklar jadvali bo'shagan joyni oladi (1366×768 da 2–3 qator o'rniga 11 qator).
- Sarlavha 28px; yig'ilganda yonida `Jami tushum · N ta chek · Sotilgan` bir qatorda qoladi (foyda ko'rsatilmaydi — u Admin PIN ostida).
- Animatsiya 240 ms: balandlik 0 ga tushadi, ichidagilar pastga yopishgani uchun yuqoriga chiqib ketadi (`ReportsView.xaml.cs` → `ApplyStatsState`).
- Holat `app_settings` da `reports_stats_collapsed` kaliti bilan saqlanadi (`ReportsViewModel.IsStatsCollapsed`), dastur qayta ochilganda animatsiyasiz tiklanadi.
- 46 test o'tdi; render'da ochiq / yig'ilgan / qayta ochilgan holatlar tekshirildi. Animatsiyaning silliqligi faqat haqiqiy ekranda ko'rinadi.

**2026-11-15 (Claude Code) — Ombor kartasidagi `Tan narxi` (2.1):**
- Kartada `Tan narxi` yozuvi ostida qoldiq × **sotish narxi** chiqayotgan edi (eski versiyada bu son `Qiymati` deb yozilgan). Egasi Excel hisoboti bilan solishtirib topdi: 17 651 000 (ekran) va 12 048 200 (Excel).
- Endi `Warehouse.TotalStockCost` = qoldiq × tan narxi, dollarli tovarlar joriy kurs bo'yicha — Excel `Ombor tan narxi` hisoboti bilan bir xil formula (manfiy qoldiqlar ham kiradi). `TotalStockValue` (sotuv qiymati) saqlandi, lekin ekranda ishlatilmaydi.
- 2 ta yangi test (`WarehouseStockCostTests`), jami 46 o'tdi.

**2026-11-15 (Claude Code) — Kam qolgan tovarlar: kategoriya bo'yicha filtr (2.2):**
- Eski versiyada (1.0.1) bu tugma bor edi, lekin ViewModel qismi yozilmagani uchun ishlamas edi; `hotfix/lowstock-filter-1.0.2` da tuzatildi va shu yerga ko'chirildi. Maketda bunday tugma yo'q — qoldiq filtri (`Hammasi / Kam qolgan / Tugagan`) yoniga maket uslubida qo'shildi.
- `InventoryViewModel`: `LowStockCategoryFilters`, `IsCategoryFilterDropdownOpen`, `SelectedCategoriesFilterText`, `HasLowStockCategoryFilter`, `ClearCategoryFiltersCommand`.
- Jadval sarlavhasidagi "hammasini belgilash" katakchasi ham ishlamas edi (`IsAllLowStockSelected` yo'q edi) — qo'shildi, faqat ko'rinib turgan qatorlarni belgilaydi.
- 7 ta yangi test (`LowStockCategoryFilterTests`), jami 44 o'tdi. Render'da tekshirildi, egasi hali ko'rmagan.

**2026-11-15 (Claude Code) — egasi ko'rib chiqqandan keyingi tuzatishlar:**
- Tovar formasi (2.3): `dona / metr / kg` tanlovi bosilganda o'zgarmas edi. Sabab: `SegItem` uslubida `Focusable=False` — WPF `ListBoxItem` fokus ololmasa bosishni tanlov deb hisoblamaydi. Olib tashlandi. Shu uslubdagi boshqa tanlov yo'q (qolgan `Focusable=False` joylar tugma, belgi yoki tanlovsiz ro'yxatlar).
- Dastur nomi **Line kassa**: yuqori panelda `S` plitka va `SMART` yozuvi o'rniga logo rasmi (`LOGO/logos/4_windows` → `Assets/app_logo.png`, `.ico`); oyna sarlavhasi, aktivatsiya sahifasi, sinxron matnlari, zaxira fayl nomi (`LineKassa_Baza_…`), chek va qarz ko'chirmasi sarlavhasi (`LINE KASSA`).
- O'zgartirilmadi: QR ichidagi `name = "SMART Kassa"` va Firewall qoidasi nomlari (sinxron mantig'i), Android ilova.
- Kassa savati: to'lov paneli och kulrang fon (`Surface2`), tepasida 2px `LineStrong` chiziq va ro'yxat ostida soya.
- Egasi fikri: dizayn ma'qul, boshqa xato ko'rmadi. Build va 37 test o'tdi; render'da tekshirildi, haqiqiy ekranda hali ko'rilmagan.

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
- 2.4 o'chirish tasdig'i: MessageBox o'rniga maketdagi oyna (`IsDeleteConfirmOpen`, `ConfirmDeleteProductCommand`).
- 2.5 kirim oynasi: `QuickStockAddDialog` maket tuzilishida (yorug' tema, chegarasiz oyna).
- 2.6/2.7 stiker oynasi: `BarcodePrintDialog` maket tuzilishida. `FieldCombo` uslubi `DisplayMemberPath`ni hisobga olmasdi (sinf nomi chiqardi) — tuzatildi.
- 2.8/2.9 stiker jarayoni: `MainWindow`dagi oyna va vidjet maket tuzilishida; `LabelPrintService` o'zgarmadi.
- Test eslatmasi: `ReportsViewModelSearchTests.Search_lists_newest_first...` yarim tundan keyingi 3 soat ichida yiqiladi ("bugun, 3 soat oldin" kechagi kunga tushadi) — dizaynga aloqasi yo'q, testning o'zidagi kamchilik.
- 2.10–2.13: buyurtma ro'yxati oynasi va Ombordagi uchta modal (ko'chirish, yangi ombor, kategoriya) maket tuzilishida. Ombor bo'limi (2.x) to'liq ko'chirildi.
- 3.1–3.7 Qarzlar: `DebtsView` to'liq maket tuzilishida. VM'da faqat bloklash tasdig'i ekran ichidagi oynaga ko'chdi; DTO'ga ikkita ko'rsatish xossasi (`PaidUz`, `MethodDisplay`) qo'shildi. To'lov, qaytarish, o'tkazish logikasi o'zgarmadi.
- **Keyingi qadam:** maketdagi barcha ekranlar ko'chirildi (🟡). Egasi ekranda ko'rib chiqadi → ✅. Keyin: to'q tema ranglari, ko'p tillilik, raqam formati (`12 000`), 3.8 hisob-kitob varaqasi.

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

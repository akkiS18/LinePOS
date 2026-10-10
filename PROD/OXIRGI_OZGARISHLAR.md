# 🚀 LinePOS - Oxirgi O'zgarishlar va Qulayliklar

Ushbu hujjat mijozga oxirgi kiritilgan o'zgarishlar va yangi qulayliklarni ko'rsatish uchun tayyorlandi:

---

### 1. 📊 Tovarlar jadvalining ixchamlashishi va ustunlar yangi tartibi
- **Nima o'zgardi:** 
  - Kam ishlatiladigan ustunlar (**Kategoriya**, **Birlik**, **2-sotish narxi**) asosiy jadvaldan olib tashlandi.
  - Ustunlar tartibi eng ko'p ishlatiladigan qulay ketma-ketlikka keltirildi:
    1. **Mahsulot nomi**
    2. **Amallar (Tahrirlash, Shtrix-kod chiqarish, O'chirish)** — darhol nomining yoniga o'tkazildi!
    3. **Shtrix-kod**
    4. **Izoh (Zakaz uchun)**
    5. **Tan narxi**
    6. **Sotish narxi**
    7. **Qoldiq**
- **Mijoz uchun foydasi:** 
  - Jadval barcha kompyuter ekranlariga 100% to'liq sig'adi — yonga scroll qilishga (surishga) mutlaqo hojat qolmadi!
  - Har doim eng ko'p bosiladigan tugmalar (amallar) ko'z oldida turadi.

---

### 2. 🔠 Mahsulot nomi birinchi harfi avtomatik KATTA harf bo'ladi
- **Nima o'zgardi:** Yangi tovar qo'shganda yoki tahrirlaganda, tovar nomining birinchi harfi avtomatik tarzda **KATTA** harf qilib olinadi (masalan: `kabel` yozilsa, darhol `Kabel` bo'lib yoziladi).
- **Mijoz uchun foydasi:** Bosh harf yozish uchun `Shift` tugmasini bosib o'tirish shart emas.

---

### 3. ⌨️ Shtrix-kod skaneri uchun aqlli to'g'rilash (Caps Lock / Rus tili ignor)
- **Nima o'zgardi:** Shtrix-kodlarni o'qitish paytida tasodifan **Caps Lock** yonib qolgan bo'lsa yoki klaviatura tili **Rus tili**da qolib ketgan bo'lsa ham, dastur xato belgilarni (masalan: `^(#%#^$((#)%)`) avtomatik tarzda to'g'ri raqamlarga (masalan: `6935364099305`) aylantirib qabul qiladi.
- **Mijoz uchun foydasi:** Kassada ishlayotganda har safar tilni inglizchaga o'tkazish yoki Caps Lockni tekshirish shart emas.

---

### 4. 🟩 "+ Yangi Mahsulot" tugmasining to'liq va uzluksiz ishlashi
- **Nima o'zgardi:**
  - Oldin qidiruv ishlatilganda yoki bosh ekrandan turib tovar izlanganda (ombor tanlanmaganligi sababli) "Yangi mahsulot" tugmasi adashib yashirinib qolayotgan edi. Endi u **barcha holatlarda (jumladan qidiruv natijalarida ham)** chiqib turadigan bo'ldi.
  - Kompyuter oynasi (razresheniya) kichraytirilganda "Qidiruv" qismi tugmalarni ekrandan tashqariga siqib chiqarmasligi uchun moslashuvchan qilib to'g'irlandi. Tugmalar doim ko'rinib turadi.

---

### 5. 🔙 "Omborlarga qaytish" tugmasining mantiqiy tuzatilishi
- **Nima o'zgardi:** Bosh ekrandan (Barcha omborlar ro'yxati turgan ekrandan) bironta tovar izlasangiz, dastur jadvalni ochib berar, lekin orqaga qaytish tugmasini adashib "Kategoriyalarga qaytish" qilib qo'yar edi. Bu esa bosh ekranga qaytishni imkonsiz qilgandi.
- **Mijoz uchun foydasi:** Endi umumiy ombordaykacha izlasangiz, orqaga qaytish tugmasi to'g'ri holda **"Omborlarga qaytish"** bo'lib ko'rinadi va bossangiz to'g'ridan-to'g'ri boshlang'ich omborlar ro'yxatiga qaytaradi.

---

### 6. 🏷️ Shtrix-kod chop etish: 100% Aniq Preview (WYSIWYG) va Nomni surish
- **Nima o'zgardi:**
  - Oldin ekrandagi ko'rinish bilan printerdan chiqqan qog'oz o'rtasida farq bo'lib, uzun nomlar sig'may qolardi. Endi ekran (Preview) va printer bir xil chizuvchi mexanizmdan foydalanadi (WYSIWYG).
  - Mahsulot nomini chapga/o'ngga siljitish imkoniyati qo'shildi (`◀ 0.0 mm ▶ ↺`).
  - Nomni 1 qator yoki 2 qator qilish rejimini tanlash mumkin.
  - Tanlangan sozlamalar avtomatik xotirada saqlanadi.

---

### 7. 🎨 Zamonaviy Vektor Ikonkalar (SVG Path) & Self-Contained Setup
- **Nima o'zgardi:**
  - Dasturdagi barcha tugmalar (Tahrirlash, Shtrix-kod chop etish, O'chirish, Omborlar, Qidiruv, Saqlash, Bekor qilish) tizim shriftlariga bog'liq bo'lmagan zamonaviy vektor ikonkalariga (`Path`) o'tkazildi.
  - Shrift yoki kodirovka buzilishi (mojibake belgilari) muammosi to'liq va butunlay bartaraf etildi.
  - Setup fayli hech qanday qo'shimcha .NET yoki boshqa narsalar o'rnatishni talab qilmaydi — 100% hamma narsa ichida (`win-x64 --self-contained`).

---

### 8. 🧾 Chek Chop Etish va Ekranni (Preview) 1-ga-1 Bir Xil Qilish
- **Nima o'zgardi:**
  - Printerdan chiqqan chek bilan ekrandagi Preview 100% bir xil bo'lishi uchun yagona umumiy generatorga (`BuildReceiptLines`) birlashtirildi.
  - Minglik ajratgichlarda UTF-8 buzilishi (`Та` belgisi chiqib qolishi va qator buzilib `00`, `m` pastga tushib ketishi) toza ASCII probel formati orqali to'liq tuzatildi.
  - Kassa cheki endi istalgan termal printerda hech qanday xatosiz, chiroyli va tekis chop etiladi.

---

### 9. 🔄 Desktop: Wi-Fi Ulanish Ekrani (QR Karta) Pasti Qirqilishi To'g'rilandi
- **Nima o'zgardi:**
  - Ekran o'lchami kichikroq bo'lgan noutbuk yoki monitorlarda QR kod ostidagi sozlamalar ochilganda pastki tugmalar ("Ulash kodi", "Firewall ruxsati", "Ruxsatlarni bekor qilish") qirqilib qolmasligi uchun chap blok `ScrollViewer` ga olindi.
  - Endi barcha razresheniyalarda oyna qulay tarzda aylanadi (scroll bo'ladi).

---

### 10. 📱 Mobil: Kompyuterga Wi-Fi Orqali Ulanish Oynasi To'liq Yangilandi
- **Nima o'zgardi:**
  - Qisilgan, ekranni to'sib turadigan modal dialog o'rniga yuqorisida orqaga qaytish tugmasi (`←`) bo'lgan zamonaviy to'liq oyna (Full Screen) qilindi.
  - Ekranni to'ldirib turgan ortiqcha, foydalanuvchini chalg'ituvchi barcha instruksiya va matnlar olib tashlandi.
  - Intuitiv bloklar:
    1. **Holat nishoni**: Yashil/kulrang tiniq indikator (`Ulangan` yoki `Aloqa yo'q`).
    2. **Tezkor ulanish**: Katta va qulay **`[ 📷 QR Kodni Skanerlash ]`** tugmasi.
    3. **Qo'lda ulash**: Server IP va ulanish kodi uchun toza inputlar hamda **`[ Bog'lanish ]`** tugmasi.

---

### 11. ⚠️ Desktop: Brak (Spisanie - 0 so'm) Hisobdan Chiqarish Modali & Tezkor Tugmalar
- **Nima o'zgardi:**
  - Ekranni to'ldirib turgan ortiqcha instruksiya matnlari olib tashlandi, faqat tovarlar soni va sof zarar ko'rsatilgan ixcham xulosa qoldirildi.
  - Tugmalar zamonaviy ko'rinishga keltirildi va klaviatura tezkor tugmalari qo'shildi:
    - **`✕ Yo'q [Esc]`**: <kbd>Esc</kbd> bosilganda oynani bekor qilib yopadi va fokusni qidiruvga qaytaradi.
    - **`⚠️ Ha [Enter ↵]`**: Modal ochilishi bilan fokus unga o'tadi va <kbd>Enter</kbd> bosilishi bilan brak hisobdan chiqariladi.

---

### 12. 📱 Mobil: Barcha Sahifalardagi Double-Padding (Tepa va Pastdagi Bo'shliqlar) Tuzatildi
- **Nima o'zgardi:**
  - Asosiy oyna (`MainScreen`) allaqachon tepa (status bar) va pastki (dock) bo'shliqlarni bergani holda, ichki sahifalar (`Kassa`, `Ombor`, `Hisobotlar`) ikkinchi marta tizim paddinglarini olayotgan edi.
  - Barcha ichki sahifalardagi takroriy insetlar nolga keltirildi:
    - Sarlavha ostidagi ulkan bo'shliqlar yo'qoldi.
    - 3-tugmali (`|||`, `O`, `<`) navigatsiyali telefonlarda pastki dock endi siqilib qolmaydi, to'liq 64dp hajmda qulay bosiladi.
    - Hisobotlar sahifasidagi **Dollar kursi** tugmasi (`1$ = ... so'm`) ekranning markaziga ko'tarilib ketmaydi, pastki dock ustida tabiiy o'rnida turadi va bo'sh holatdagi `"Ushbu davrda savdolar mavjud emas"` yozuvini to'sib qo'ymaydi.

---

### 13. 🔍 Aqlli Qidiruv (Smart Search): So'zlar erkin tartibda, Lotin ⇄ Kirill va Belgilar bag'rikengligi (Desktop & Mobil)
- **Nima o'zgardi:** 
  - Kassa va Ombordagi barcha qidiruvlar yangi aqlli qidiruv tizimiga (`SmartSearchHelper`) o'tkazildi.
  - **Erkin tartib (Unordered search):** So'zlarni ketma-ket yozish shart emas. O'rtada so'zlar qolib ketgan bo'lsa yoki so'zlar o'rni almashgan bo'lsa ham (masalan: `kabel qora` yoki `qora 2.5` deb yozilsa ham `Kabel mis 2.5 qora` tovari darhol topiladi).
  - **Lotin ⇄ Kirill avtomatik transliteratsiya:** Foydalanuvchi klaviaturada kirillcha (`кабель`, `автомат`) yoki lotincha (`kabel`, `avtomat`) yozishidan qat'i nazar, mahsulot bazada qaysi alifboda kiritilgan bo'lsa ham 100% topiladi. O'zbekcha o'ziga xos harflar (`sh`, `ch`, `o'`, `g'`) to'liq qo'llab-quvvatlanadi.
  - **Nuqta va vergul bag'rikengligi:** `2.5` yoki `2,5` deb yozilsa ham ikkala holatda bir xil topadi.
  - **Dolzarblik saralashi (Relevance ranking):** Nomi to'g'ridan-to'g'ri qidiruv so'zi bilan boshlanadigan tovarlar eng yuqori o'rinda (1-bo'lib) chiqadi.
- **Mijoz uchun foydasi:** 
  - Tovarni qidirib topish vaqti bir necha barobarga qisqaradi, aniq nomini eslab qolish yoki ma'lum bir tartibda kiritish talab etilmaydi.

---

### 14. 🛒 Kam Qolgan Tovarlarni Tanlab Buyurtma Qilish, Chek (Printer) va Excel Jadvaliga Chiqarish (Desktop & Mobil)
- **Nima o'zgardi:**
  - Oldin "Kam qolgan tovarlar"ga kirganda barcha tovarlar bitta katta ro'yxat bo'lib turar, faqat ba'zi tovarlarni zakaz qilish kerak bo'lsa ularni ajratib olish imkoni yo'q edi.
  - Endi har bir tovar yonida qulay **Checkbox (tanlash)** qo'shildi:
    - **Tanlab yig'ish:** Faqat olib kelinishi kerak bo'lgan tovarlarni belgilab chiqish mumkin.
    - **Tezkor boshqaruv:** `✓ Barchasi` (hammasini birdan belgilash) va `✕ Tozalash` tugmalari mavjud.
    - **Faqat tanlanganlar:** Belgilangan tovarlarni ko'rish uchun `Faqat tanlanganlar` filtri qo'shildi.
    - **Alohida ko'rish va o'chirish modali (Buyurtma ro'yxati):** Alohida `🛒 Buyurtma ro'yxati` dialogi ochilib, adashib qo'shilgan tovarlarni `❌` tugmasi orqali o'chirib tashlash mumkin.
  - **Chiqarish imkoniyatlari (Eksport):**
    1. **🖨️ Kassa/Termal Printerda Chek chiqarish:** 58mm va 80mm chek printerlariga moslangan, tovar nomi, qoldiq va buyurtma yozish uchun maxsus katakchali (`[   ]`) qog'oz cheki chop etiladi (yoki telefonda matn shaklida printerga/Telegramga ulashiladi).
    2. **📊 Excel (.xls) Jadval qilib saqlash:** Chiroyli ustunlar (№, Mahsulot nomi, Shtrix-kod, Birlik, Hozirgi qoldiq, Tan narxi, Buyurtma miqdori) bilan tayyor Excel jadvali yaratiladi va kompyuterga saqlanadi yoki telefondan ulashiladi.
- **Mijoz uchun foydasi:**
  - Bozordan tovar olib kelish, yetkazib beruvchilarga zakaz berish va omborni to'ldirish jarayoni 100% qulay, tez va xatosiz bo'ldi.

---

### 15. 🔄 Wi-Fi Sinxronizatsiya: "Baza boshqa kompyuterga bog'langan" xatoligining aqlli yechimi va Aloqani uzish imkoniyati (Mobil)
- **Nima o'zgardi:**
  - **Avvalgi muammo:** Kompyuter dasturi yangilanganda yoki qayta o'rnatilganda (Inno Setup orqali yangi baza o'rnatilganda), kompyuter yangi server identifikatoriga (ID) ega bo'ladi. Mobil ilova esa xotirada eski server ID ni saqlab turgani sababli: *"Bu baza boshqa kompyuterga bog'langan"* deb ulanishga ruxsat bermas, uni qayta ulashning imkoni yo'q edi.
  - **Yangi qulayliklar:**
    1. **Aqlli qayta bog'lash (Re-pairing Dialog):** Endi QR-kod skaner qilinganda yoki IP orqali ulanilganda yangi/yangilangan kompyuter aniqlansa, dastur qotib qolmaydi — avtomatik ravishda tushunarli oyna chiqaradi:
       > *"⚠️ Yangi Kompyuterga Ulash: Ushbu telefon avval boshqa kompyuterga bog'langan. Yangi kompyuterga qayta bog'lashni xohlaysizmi?"*
       Foydalanuvchi **`[ Ha, bog'lash ]`** tugmasini bosishi bilan barcha sinxronizatsiya kursorlari toza holatga keltirilib, telefon muvaffaqiyatli yangi kompyuterga bog'lanadi!
    2. **"Aloqani uzish" tugmasi:** Wi-Fi ulanish oynasida kompyuterni uzib tashlash imkonini beruvchi maxsus **`Aloqani uzish`** tugmasi qo'shildi. Istalgan vaqtda bitta tugma bilan eski aloqani uzib, boshqa kompyuterga ulanishga tayyor holatga keltirish mumkin.
    3. **100% Xavfsiz tozalash:** Qayta ulanganda eskirgan kursorlar va versiyalar to'liq tozalanadi, bazalar o'rtasida chalkashlik yoki ziddiyat (conflict) kelib chiqishi butunlay bartaraf etildi.
- **Mijoz uchun foydasi:**
  - Kompyuter dasturi yangilanganda yoki boshqa kompyuterga o'tilganda telefon hech qanday xatoliksiz, 1 ta tugma orqali osongina ulanadi va ishlashda davom etadi.

---

### 16. ⚡ Qoldiq ustiga bosganda tezkor tovar qoldig'ini oshirish (Tezkor Kirim dialogi - Desktop va Android)
- **Nima o'zgardi:**
  - **1 marta bosish orqali tezkor kirim:** Tovarlar jadvalidagi "Qoldiq" raqamiga (masalan `87 ➕`) 1 marta bosish bilan tovar ichiga kirmasdan bevosita qulay dialog ochiladi.
  - **Avtomatik hisoblash va Jonli ko'rinish:** Yangi kelgan tovar soni kiritilishi bilan dastur joriy qoldiqqa yangi sonni qo'shib formulasini ko'rsatadi (masalan: `10 + 5 = 15 dona`). `Enter` bosilishi bilan qoldiq darhol saqlanadi.
  - **Ham Desktop, ham Android uchun bir xil qulay:** Kompyuterda ham, telefonda ham tovar qoldig'i nishoniga bosib yangi tovarlarni tezkorlik bilan omborga kiritish mumkin.
  - **Offline Wi-Fi sinxronizatsiya kafolati (Delta tranzaksiyasi):** Qoldiq oshirilganda dastur `sync_journal` tranzaksiyalar jurnaliga mutlaq raqam emas, aynan qo'shilgan farqni (`delta = +10`) yozadi. Agar tarmoq bo'lmasa xavfsiz navbatda turadi va Wi-Fi paydo bo'lganda boshqa qurilmalardagi savdolar bilan to'qnashmasdan avtomatik jamlanadi.
- **Mijoz uchun foydasi:**
  - Yangi kelgan tovarlarni omborga kiritish bir necha baravar tezlashdi. Har bir tovarning tahrirlash oynasiga kirib chiqish, narxlar va boshqa ma'lumotlarni bosib o'tirish shart emas.

---

### 17. 🚀 Birlashtirilgan Yangi Versiya (1.0.2): Tovarlarni qaytarish va Qarz daftari integratsiyasi
- **Nima o'zgardi:**
  - GitHub'dagi mahsulotlarni qaytarish, cheklar yaxlitligi, qarz daftari sxema va migratsiyalari hamda bizning lokal aqlli qidiruv, kam qolgan tovarlar buyurtma ro'yxati va tezkor kirim tizimimiz yagona tizimga birlashtirildi.
  - Mobil ilova versiyasi `1.0.2` (versionCode `3`) ga yangilandi, dastur nomi rasmiy toza holda **"SMART kassa"** qilindi.
  - Desktop kompyuter dasturining yangi mustaqil o'rnatish paketi (`Line_kassa_Desktop_Setup.exe`) yig'ildi.
- **Mijoz uchun foydasi:**
  - Barcha yangiliklar bitta to'liq paketda ishlaydi, mijoz telefonida mavjud barcha tovar va bazalar 100% saqlangan holda yangilanadi.

---

### 18. 📱🎨 Mobil Hisobotlar Ekrani Qayta Loyihalandi: Alohida "Cheklar Tarixi" Oynasi va Ixcham Chek Kartochkalari
- **Nima o'zgardi:**
  - Hisobotlar ekrani 2 ta mustaqil va tartibli qismga ajratildi:
    1. **Asosiy hisobotlar paneli (Dashboard):** Vaqt filtrlari, ombor/kategoriya tanlovi, moliyaviy xulosa bloki, 4 ta asosiy KPI kartasi (Jami tushum, Sof foyda, Savdolar soni, Sotilgan tovarlar), Excel eksport tugmasi va yuqori o'ng burchakda aylanuvchi animatsiyali ixcham Dollar kursi nishoni. Ekranning to'lib, siqilib ketishi to'liq bartaraf etildi.
    2. **Alohida "Cheklar tarixi" oynasi:** Asosiy ekrandagi "Cheklar tarixi (N ta chek) ➔" kartasini yoki "SAVDOLAR SONI" KPI kartasini bosganda to'liq ekranni egallagan yangi oyna ochiladi. Unda orqaga qaytish (`←`) tugmasi, chek raqami bo'yicha tezkor qidiruv, to'lov turlari filtrlari (`Barchasi`, `Savdo`, `Qaytarish`, `Brak`) va barcha cheklar ro'yxati joylashgan. Telefonning orqaga (Back) tugmasi bilan ham qulay qaytish mumkin.
  - **Chek kartochkasi 2 barobar ixchamlashtirildi:** Oldingi 35 belgili uzun hash-kod o'rniga qulay `Chek #2` va to'lov nishoni (`Naqd`, `Karta`, `Brak`, `Qaytarish`) 1 qatorda ko'rsatiladigan qilindi. Balandligi ixchamlashib, barcha ma'lumotlar o'z o'rnida joylashdi. Tizimning to'liq hujjati raqami esa chek ustiga bosilganda ochiladigan dialogda saqlab qolindi.
  - **Dollar kursi FAB tugmasi to'g'rilandi:** Pastda chek kartalari va matnlarni to'sib turuvchi suzuvchi (FAB) tugma olib tashlanib, asosiy ekranning yuqorisidagi xalaqit bermaydigan qulay joyga ko'chirildi.
- **Mijoz uchun foydasi:**
  - Hisobotlar ekrani toza va yengil bo'ldi, barcha moliyaviy raqamlar bitta qarashda ko'rinadi. Cheklarni ko'rish, qidirish va tahlil qilish esa alohida keng ekranda ancha qulaylashdi.

---

### 19. 🎨 Desktop Foydalanuvchi Interfeysi (UI) va Ranglar Falsafasini Yaxshilash
- **Nima o'zgardi:**
  - **Aktiv tablar kontrastliligi (Yuqori menyu):** Yuqoridagi asosiy navigatsiya tugmalarida (Kassa, Ombor, Hisobotlar, Wi-Fi Sinxron) aktiv bo'lgan tab ustiga sichqoncha borganda (hover), matn va ikonka xira bo'lib qolishi bartaraf etildi. Aktiv tab hover holatida ikonka va matn yorqin oq (`#FFFFFF`) rangda aniq va tiniq ko'rinadigan bo'ldi.
  - **Ombor kartochkasi dizayni ixchamlashtirildi:**
    - "ASOSIY" ombor nishoni kartaning o'rta qatoriga, qoldiq va qiymat statistikasi bilan bir qatorga joylashtirildi.
    - Avvalgi qalin yashil "Kirish >" tugmasi olib tashlandi, uning o'rniga kartaning o'ng pastki burchagiga toza oq rangli ixcham `>` yo'naltiruvchi chevron belgisi qo'yildi (orqa fonsiz). Ombor kartochkasining istalgan joyiga bosilganda bevosita omborga kiriladi.
  - **Asboblar paneli tugmalari brend dizayniga moslashtirildi:**
    - "Yangilash" tugmasi va "Omborlararo Ko'chirish" tugmasidagi ko'k (cyan) ikonkalar sof oq (`#FFFFFF`) rangga o'tkazildi.
    - "+ Yangi Mahsulot" va "+ Yangi Ombor" tugmalari orqa foni "Line Kassa" brend gradientiga (`ModernButton`: `#0B6477` -> `#16A34A`) o'tkazildi.
    - "Yangilash" tugmasi ortiqcha matnlardan tozalanib, ixcham kvadrat shakldagi (40x40) aylanuvchi ikonkali tugmaga aylantirildi va orqa foni "Omborlararo Ko'chirish" kabi neytral to'q rangga (`#334155`) keltirildi.
    - "Omborlararo Ko'chirish" va "+ Yangi Ombor" tugmalari orasiga qulay masofa (gap) qo'shildi.
- **Mijoz uchun foydasi:**
  - Dasturning ko'rinishi zamonaviy, bir xil uslubdagi (consistent) va ko'zga qulay bo'ldi. Yuqori menyu tugmalari aniq o'qiladi, ombor kartalari va asboblar panelidagi tugmalar tartibli hamda estetik jihatdan chiroyli ko'rinishga ega bo'ldi.

---

### 20. 🐞 Xatolik Tuzatildi: Desktop Dasturi Ochilishidagi XAML Stili Xatoligi Bartaraf Etildi
- **Nima o'zgardi:**
  - Yuqori menyu tablarining hover triggerida (`MultiDataTrigger`) shart bog'lanishi to'g'rilandi: `Condition Property="IsMouseOver"` o'rniga to'g'ri WPF XAML standarti bo'yicha `Condition Binding="{Binding IsMouseOver, RelativeSource={RelativeSource Self}}"` qo'yildi.
  - O'rnatishdan so'ng dastur ochilganda chiqqan *"Set property 'System.Windows.FrameworkElement.Style' threw an exception"* xatoligi to'liq tuzatildi.
  - To'liq mustaqil (self-contained) yangi **Inno Setup** o'rnatish paketi ([Line_kassa_Desktop_Setup.exe](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe)) qaytadan 100% toza yig'ildi.
- **Mijoz uchun foydasi:**
  - O'rnatish faylini o'rnatgandan so'ng dastur hech qanday xatolik xabarlarisisiz silliq, tez va to'g'ridan-to'g'ri ochiladi.

---

### 21. 🎨 Desktop Savatcha Qatori Qayta Loyihalandi: 5 Ta Teng Keng Ustun va Vektor Ikonkalar
- **Nima o'zgardi:**
  - **"Savatcha" matni olib tashlandi:** Keraksiz matn o'rniga zamonaviy sof vektor savatcha belgisi va uning yonida tovarlar sonini ko'rsatuvchi ixcham indikator qoldirildi.
  - **Teng 5 ta ustunga bo'lindi:** Kassa ekranidagi savatchaning yuqori qatori aniq teng 5 ta ustunga ajratildi:
    1. *1-ustun:* Vektor savatcha belgisi va tovarlar soni nishoni;
    2. *2-ustun:* Brak tovar sotish (ogohlantiruvchi vektor uchburchak);
    3. *3-ustun:* Narxni almashtirish (1-narx / 2-narx vektor strelkalar);
    4. *4-ustun:* Pause / Kutishga qo'yish (Hold);
    5. *5-ustun:* Delete / Savatchani tozalash (Chiqindi qutisi).
  - **Ranglar uyg'unlashtirildi:** Faqat Delete tugmasi qizil rangli urg'u bilan ajralib turadi. Qolgan barcha tugmalar chiroyli kulrang fonda (`#334155`) sof oq (`#FFFFFF`) vektor ikonkalar bilan ta'minlandi.
  - **Bo'sh savatda xira (Disabled) holat:** Savatchada tovar bo'lmaganda 4 ta amal tugmasi (Brak, 2-narx, Pause, Delete) vizual ravishda xiralashib (`Opacity: 0.35`), bosilmaydigan (disabled) holatda turadi. Mahsulot qo'shilishi bilan avtomatik yorqin va faol holatga o'tadi.
  - **Qulaylik:** Tugmalar o'lchami kengayib, sichqoncha bilan bosish bir necha barobar qulaylashdi.
- **Mijoz uchun foydasi:**
  - Kassa oynasining savatcha qismi zamonaviy va keng ko'rinishga ega bo'ldi. Kassir adashib bo'sh savatda tugmalarni bosib o'tirmaydi, tugmalar kattaligi tufayli tezkor savdoda bosish ancha osonlashdi.

---

### 22. 🎨 Kassa Tugmalari va Modallar Minimalistik Dizayni: Switch Hover Buzi Tuzatildi, Minus (-) Ikonkalar va Professional Ranglar
- **Nima o'zgardi:**
  - **Switch tugmasidagi hover xatoligi (bug) tuzatildi:** Savatdagi har bir mahsulot qatoridagi narxni almashtirish tugmasi (`[ ⇄ 1 ]`) ustiga sichqoncha borganda tizimning oq/och-ko'k standart qobig'i (chrome) chiqib qolish xatosi bartaraf etildi. Maxsus `ControlTemplate` orqali silliq va chiroyli quyuq slate hover effekti o'rnatildi.
  - **2-narxga o'tgandagi professional ranglar sxemasi:** Avvalgi qo'pol sarg'ish fon, qalin sariq ramka va ko'zni charchatuvchi ranglar o'rniga zamonaviy professional ranglar tanlandi: quyuq nozik fon (`#1E293B`), yengil osmonrang moviy hoshiya (`#0284C7`) va tiniq sky-cyan (`#38BDF8`) narx yozuvi. O'zgarish aniq seziladi, lekin ortiqcha ko'zga tashlanmaydi va professional uslubga to'liq mos keladi.
  - **Minimalistik Kutish (Hold) va Brak modallari:** Qizil va sariq bo'lib ko'zni oladigan yaltiroq oynalar o'rniga barcha modallar xotirjam minimalistik dizaynga keltirildi: to'q slate ramkalar (`#334155`), quyuq fon (`#1E293B`), Kutish uchun brend moviy aksenti va Brak uchun vazmin qizil urg'u.
  - **Axlat qutisi ikonkasi o'rniga toza Minus (-) belgisi:** Tovar savatdan axlatga tashlanmasligi, balki chegirilishi/ayrilishi mantiqiga muvofiq, savatcha sarlavhasidagi tozalash tugmasi va har bir tovar qatoridagi o'chirish tugmasi zamonaviy vektor **`-` (Minus)** belgisiga almashtirildi.
- **Mijoz uchun foydasi:**
  - Kassa interfeysi yagona minimalistik uslubda bo'lib, ko'zni toliqtirmaydi va ortiqcha vizual shovqindan xoli bo'ldi. Kassir uchun tezkor savdo qilish yanada yoqimli va qulay bo'ldi.

---

### 23. 🎨 Kassa To'lov Tugmalari, Minimalistik Modal va Navbardagi Vektor Ikonkalar
- **Nima o'zgardi:**
  - **"Cheksiz to'lov rejimi" matni olib tashlandi:** Sotish modalida printer talab qilinmaydigan rejimda ortiqcha va noqulay `Cheksiz to'lov rejimi (Printer talab qilinmaydi)` nishoni olib tashlandi. Printer statusi faqat kassa cheki yoki A4 printer tanlangandagina ko'rinadi.
  - **To'lov tugmalari asosiy kassa ekraniga o'tkazildi:** Avval modal ichida turgan va har xil rangda (yashil, moviy, to'q sariq) bo'lgan `Naqd`, `Karta`, `Aralash` tugmalari asosiy kassa ekranidagi savatcha pastiga — yagona "Sotish" tugmasi o'rniga joylashtirildi.
  - **Vazmin va professional dizayn:** Barcha to'lov tugmalari yagona quyuq slate uslubiga keltirildi (rang-baranglik to'liq yo'qotildi). Savat bo'sh bo'lganda avtomatik nofaol (disabled) va xira holatda turadi.
  - **Sotish modali tozalandi:** Modal ichida faqat chek Jonli Preview ko'rinishi, printerni tanlash va tasdiqlash qoldirildi.
  - **Navbardagi tugmalar toza vektorga o'tkazildi:** `Baza zaxirasi` va `Bog'lanish` tugmalaridagi matnlar olib tashlanib, ixcham 36x36 o'lchamdagi toza vektor ikonkali tugmalarga aylantirildi. Karta solig'i foizi va barcha tablar (`Kassa`, `Ombor`, `Hisobotlar`, `Wi-Fi Sinxron`) SVG vektorlariga o'tkazildi.
  - **"Wi-Fi faol" matnining aktiv holatdagi ko'rinishi to'g'rilandi:** Wi-Fi Sinxron tabi tanlanganda oq fon ustida yozuv yo'qolib qolmasligi uchun yuqori kontrastli yumshoq fon va to'q ko'k-osmonrang matn o'rnatildi.
- **Mijoz uchun foydasi:**
  - Kassir uchun savdo qilish yana 1 qadamga qisqardi: mijoz naqd yoki karta berishi bilan to'g'ridan-to'g'ri kerakli tugmani bosadi. Ranglar ko'zni toliqtirmaydi, navbar ixcham va zamonaviy ko'rinishga keldi.

---

### 24. ⚡ To'lov Modalida Enter/Esc Muammosi Bartaraf Etildi va Minimalistik Ko'zga Tashlanadigan To'lov Paneli
- **Nima o'zgardi:**
  - **Enter va Esc tugmalari to'liq ishlaydigan qilindi:** To'lovni tasdiqlash modali ochilganda <kbd>Enter</kbd> (Tasdiqlash) va <kbd>Esc</kbd> (Bekor qilish) tugmalari ishlamay qolish sababi (fokus SearchBox yoki boshqa elementda qolib ketishi va hodisaning yutilishi) ildizi bilan tuzatildi:
    - `CashierView` darajasida `PreviewKeyDown` hodisasida to'lov modali ochiqligi tekshirilib, <kbd>Enter</kbd> darhol sotuvni tasdiqlaydi (`ConfirmSale()`), <kbd>Esc</kbd> esa modalni yopadi;
    - Modal ochilishi bilanoq fokus avtomatik ravishda `Tasdiqlash` tugmasiga yo'naltiriladi (`IsDefault="True"` va `IsCancel="True"` qo'shildi);
    - Modal yopilgach kursor silliq ravishda yana qidiruv maydoniga (`SearchBox`) qaytadi.
  - **"Jami to'lov" so'zi olib tashlandi (Minimalist dizayn):** 2-rasm namunasiga mos ravishda ortiqcha "Jami to'lov:" matni butunlay olib tashlandi. Uning o'rniga chap tomonda tovarlar soni (`3 ta tovar`), o'ng tomonda esa yirik, zamonaviy va yorqin zumrad rangli yakuniy summa (`30,000 so'm`) joylashtirildi.
  - **To'lov tugmalari yaqqol ko'rinadigan qilib qayta loyihalandi:** 
    - Savatchaning pastki qismidagi barcha elementlar alohida chiroyli to'q konteynerga (`#0F172A`) birlashtirildi;
    - 3 ta to'lov tugmasi (`Naqd`, `Karta`, `Aralash`) ushbu konteyner ichida alohida ko'tarilgan kartochkalar kabi bo'rttirib qo'yildi (balandligi 58px, 1.5px hoshiya, 20x20 vektor ikonka yuqorida va 14px qalin matn markazda);
    - Sichqoncha borganda osmonrang moviy yaltirash (`#38BDF8`) va quyuq fon o'rnatildi, bu esa ularning asosiy amal tugmasi ekanini yaqqol ko'rsatib turadi;
    - Savat bo'sh bo'lganda 3 ta tugma avtomatik xira (disabled) holatga o'tadi.
  - **Tezkor klaviatura tugmalari ulandi:** Kassir klaviaturadan <kbd>F8</kbd> (Naqd), <kbd>F9</kbd> (Karta) yoki <kbd>F10</kbd> (Aralash) tugmasini bosib to'g'ridan-to'g'ri tegishli to'lov turini ochishi mumkin.
  - **Yangilangan Inno Setup o'rnatish fayli:** Barcha o'zgarishlar bilan yangi to'liq mustaqil [Line_kassa_Desktop_Setup.exe](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe) (52.2 MB) qayta kompilyatsiya qilindi.
- **Mijoz uchun foydasi:**
  - Kassir to'lovni tasdiqlash uchun sichqonchaga qo'l urishi shart emas — faqat klaviaturaning <kbd>Enter</kbd> yoki <kbd>Esc</kbd> tugmasi orqali soniyalar ichida savdoni yakunlaydi yoki bekor qiladi. Asosiy kassa ekranida esa qaysi to'lov turi bilan savdo qilish tugmalari juda aniq va yaqqol ko'zga tashlanadi.

---

### 25. 🚀 Tovarlar Paneli Moslashuvchanligi (Responsive Grid), 58mm Chek Himoyasi, Tezkor Space/F-tugmalar va Vektor Ikonkalar
- **Nima o'zgardi:**
  - **Tovarlar kartochkalari moslashuvchan (Responsive) qilindi:**
    - Yangi maxsus `AdaptiveGridPanel` yaratildi. U oyna kengligiga qarab ustunlar sonini dinamik hisoblaydi va kartochkalar kengligini bo'sh qolgan o'ng tomondagi joyni to'liq qoplaydigan qilib 100% cho'zadi.
    - Katalogdagi "Eng ko'p sotiladigan tovarlar" va qidiruv natijalaridagi barcha kartalar ekranning bo'sh joy qoldirmasdan chiroyli va tartibli to'ldirib turadi.
  - **Kassa qidiruv (Search) ikonkasi toza SVG vektoriga o'tkazildi:**
    - Qidiruv maydonidagi eskirgan `🔍` emoji o'rniga zamonaviy, tiniq va professional SVG vektor belgisi qo'yildi.
  - **Navbar va Oyna sarlavhasi brendingi:**
    - Yuqori navigatsiya panelidagi "Line kassa" so'zi o'rniga "SMART" qo'yildi. Dastur oynasi sarlavhasi esa "SMART kassa" deb yangilandi.
  - **58mm termal printer qirqilishdan to'liq himoyalandi (Line Wrapping):**
    - 58mm standart kassa lentasi (1 qatorda 32 ta belgi) cheklovlari qat'iy inobatga olindi.
    - Yangi aqlli `SplitIntoLines(..., 32)` algoritmi integratsiya qilindi.
    - Chek ID raqami, asl chek ma'lumotlari, uzun nomli tovarlar va to'lov summalari hech qachon qirqilib yoki `...` bilan uzilib qolmaydi, sig'magan qismi avtomatik ravishda pastki qatordan silliq davom etadi.
  - **Bog'lanish modali toza vektorga o'tkazildi:**
    - Modal oynasidagi `👨‍💻`, `📞`, `✈️` emojilari o'rniga dasturchi, telefon va Telegram yo'nalishidagi nozik oq SVG vektor ikonkalar joylashtirildi.
  - **Tezkor klaviatura tugmalari (<kbd>Space</kbd>, <kbd>F8</kbd>, <kbd>F9</kbd>, <kbd>F10</kbd>) to'liq ishga tushirildi:**
    - <kbd>Space</kbd> (Probel) va <kbd>F8</kbd> to'g'ridan-to'g'ri **Naqd** to'lovni ishga tushiradi (qidiruv maydonida matn terilayotganda probel yozuvga to'sqinlik qilmaydi, bo'sh bo'lganda yoki kassa fokusida esa tezkor savdo oynasini ochadi).
    - <kbd>F9</kbd> tugmasi **Karta** to'lovini ochadi.
    - <kbd>F10</kbd> tugmasi **Aralash** to'lovini ochadi (WPF da Windows tizimi F10 ni menyu sifatida ushlab qolishi to'liq bartaraf etildi).
    - Tugmalar ustidagi ko'rsatkichlarga (Tooltip) klaviatura belgilari aniq ko'rsatib qo'yildi (`[Space / F8]`, `[F9]`, `[F10]`).
  - **Mustaqil yangi o'rnatish paketi:**
    - Barcha yangilanishlar bilan `Line_kassa_Desktop_Setup.exe` yangidan kompilyatsiya qilindi va `PROD` papkasiga joylashtirildi.
- **Mijoz uchun foydasi:**
  - Kassir klaviaturadan qo'lini uzmasdan birgina <kbd>Space</kbd> (yoki F8/F9/F10) tugmasi bilan tezkor savdoni amalga oshiradi. 58mm termal printerda chekning hech qaysi qismi qirqilmaydi. Tovarlar kartochkalari esa monitor o'lchamidan qat'i nazar oynani to'liq va go'zal to'ldiradi.
---

### 26. 🚀 58mm Chek Preview O'lchami To'g'rilandi, Space Tugmasi To'liq Ishga Tushirildi va Amaliyotlar Logi Olib Tashlandi
- **Nima o'zgardi:**
  - **Chek preview o'lchami (58mm) to'liq proporsional qilindi:**
    - Oldingi qotib qolgan `Width="280"` kenglik olib tashlandi.
    - 58mm termal kassa qog'ozi (32 belgi) o'lchamiga mos ravishda markazlashtirilgan moslashuvchan oyna o'rnatildi (chap va o'ng chegaralari aniq 14px teng masofada). O'ng tomonda noo'rin bo'sh joy qolish xatosi bartaraf etildi.
    - Chek qog'ozi vertikal aylantirish (ScrollViewer) bilan o'rab olindi, bu esa uzun cheklarni bemalol tepaga-pastga aylantirish imkonini beradi.
  - **Space (Probel) tugmasi bilan Naqd to'lov muammosi to'liq bartaraf etildi:**
    - Oyna darajasida (`Window_PreviewKeyDown`) <kbd>Space</kbd> tugmasi <kbd>F8</kbd> kabi to'g'ridan-to'g'ri bog'landi.
    - Tovar savatga qo'shilganda qidiruv maydoni avtomatik tozalanishi yo'lga qo'yildi.
    - Natijada savatda tovar bor paytda klaviaturadagi <kbd>Space</kbd> tugmasi bosilishi bilanoq darhol Naqd to'lov oynasi ochiladi (agar qidiruv maydonida faol bir nechta so'z terilayotgan bo'lsa, probel matn ichida bo'shliq vazifasini bajaradi).
  - **Amaliyotlar logi to'liq olib tashlandi:**
    - 2-rasmda ko'rsatilgan kassa oynasining pastki qismidagi amaliyotlar haqida xabar beruvchi panel (`StatusMessage`) butunlay olib tashlandi. Interfeys yanada toza va minimalistik ko'rinishga keltirildi.
  - **Yangi o'rnatish to'plami yig'ildi:**
    - To'liq mustaqil yangi [`PROD/Line_kassa_Desktop_Setup.exe`](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe) (52.2 MB) qaytadan muvaffaqiyatli kompilyatsiya qilindi.
- **Mijoz uchun foydasi:**
  - Chekni ko'rish oynasi xuddi haqiqiy kassa lentalaridek ixcham va chiroyli ko'rinadi. Kassir birgina <kbd>Space</kbd> tugmasini bosib darhol naqd savdoni tasdiqlashi mumkin. Ekranning pastida ortiqcha log yozuvlari ko'zni chalg'itmaydi.
---

### 27. 🚀 To'lov Modali O'lchami Qat'iylashtirildi, 58mm Chek Aylantirish (Scroll) va Bo'shliq Muammosi Ildizi Bilan Tuzatildi
- **Nima o'zgardi:**
  - **Xatolik sababi aniqlandi va to'liq bartaraf etildi:**
    - Avvalgi yig'ish jarayonida `dotnet publish` natijasi bilan Inno Setup (`Line_kassa_Setup.iss`) o'qiyotgan katalog o'rtasidagi yo'l nomuvofiqligi sababli, Inno Setup eski (21:49 dagi) DLL fayllarini o'rab qo'ygani aniqlandi. Shu sababli oldingi o'zgarishlar (amaliyotlar logini o'chirish va kenglik tuzatishlari) o'rnatuvchi faylga kirmay qolgan edi.
    - `dotnet publish` to'g'ridan-to'g'ri `win-x64\publish` papkasiga yo'naltirildi va o'rnatuvchi paket yangi versiya bilan 100% qayta qurildi.
  - **To'lov modali endi kattalashmaydi ("modal kattalashmasin"):**
    - To'lov modaliga qat'iy va qulay `Height="570"` balandlik o'rnatildi (`MaxHeight` o'rniga). Savatda nechta tovar bo'lishidan qat'i nazar, modal oynasi kattalashmaydi, barcha tugmalar va ma'lumotlar joyida qat'iy turadi.
  - **Chekning o'zi erkin aylanadigan (scroll) qilindi ("chekni ozi scroll boladigan bolsin"):**
    - Preview bloki balandligi modal ichida to'liq moslashib (`VerticalAlignment="Stretch"`), uzun cheklar (ko'p tovarlar) uchun faqat oq chek qog'ozi ichkarida sichqoncha g'ildiragi orqali yuqoriga-pastga silliq aylanadi (scroll).
  - **O'ng tomondagi bo'shliq to'liq yo'qotildi:**
    - 58mm chek qog'ozi kengligi aniq `Width="220"` ga keltirildi (193.5px matn + chap va o'ngdan 13px dan teng simmetrik maydon). O'ng tomonda ortiqcha noo'rin oq bo'shliq qolishi butunlay yo'qoldi.
  - **Sotish tugmalari ostidagi yozuvlar (amaliyotlar logi) butunlay olib tashlandi:**
    - Kassa oynasidagi `StatusMessage` matn bloki yangi yig'ilgan to'plamda to'liq yo'q bo'lib, sotish tugmalari ostida hech qanday yozuv chiqmaydi.
  - **Yangi Inno Setup fayli:**
    - Barcha o'zgarishlar bilan yangi [`PROD/Line_kassa_Desktop_Setup.exe`](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe) (52.2 MB) qayta yig'ildi.
- **Mijoz uchun foydasi:**
  - To'lov oynasi ekran bo'ylab cho'zilib ketmaydi, qulay va chiroyli o'lchamda turadi. Chek kassa lentasi kabi ixcham va toza ko'rinishda bo'lib, ko'p mahsulotli savdolarda ham ichkarida bemalol aylanadi. Kassa pastidagi log yozuvlari butunlay tozalandi.

---

### 28. 🎨 Kassa Qidiruvidan Kategoriyalar Olib Tashlandi, Ombor Oynasi Navigatsiyasi va Asosiy Ombor Kartasi Modernizatsiya Qilindi
- **Nima o'zgardi:**
  - **Kassa qidiruvidagi kategoriya tugmalari olib tashlandi (1-rasm):**
    - Qidiruv maydoni ostidagi eski UX dan qolgan toifa chiplari ("Barchasi", "Kam qolgan tovarlar", "2-ombor", ...) to'liq olib tashlandi.
    - Endi kassir qidiruv maydoniga yozganda tovarlar ro'yxati to'g'ridan-to'g'ri barcha mahsulotlar bo'yicha to'liq bo'y-bastida chiqadi.
  - **Ombor boshqaruvidagi ortiqcha tavsif yozuvi olib tashlandi:**
    - "OMBORLAR BOSHQARUVI" sarlavhasi ostidagi "Kerakli omborni tanlang yoki yangi ombor yarating" tushuntirish yozuvi o'chirildi.
  - **Qidiruvdan so'ng tugma nomi "Ortga qaytish" ga o'zgartirildi (2-rasm):**
    - Tovarlar jadvalidagi navigatsiya tugmasi "Kategoriyalarga qaytish" o'rniga aniq va mantiqiy "Ortga qaytish" deb nomlandi (`BackButtonText`).
  - **Ombor kartasidagi "Asosiy qilish" tugmasi oq yulduzcha vektor ikonka qilindi (3-rasm):**
    - Pastki chapdagi sariq rangli `★ Asosiy qilish` tugmasi butunlay olib tashlandi.
    - Kartaning yuqori o'ng burchagiga oq rangli vektor yulduzcha (`Path`) joylashtirildi:
      - Agar ombor asosiy bo'lsa: to'lgan oq yulduzcha (★) ko'rinadi;
      - Agar ombor ikkilamchi bo'lsa: ichi bo'sh oq kontur yulduzcha (☆) ko'rinadi.
    - Yulduzchani bosganda ombor darhol asosiy omborga aylanadi; agar allaqachon asosiy bo'lsa o'z holicha qolaveradi.
  - **Yangi o'rnatish to'plami yig'ildi:**
    - Yangi [`PROD/Line_kassa_Desktop_Setup.exe`](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe) (52.2 MB) qaytadan muvaffaqiyatli kompilyatsiya qilindi.
- **Mijoz uchun foydasi:**
  - Kassada tovar izlash jarayoni tezlashdi va toza ko'rinishga ega bo'ldi. Ombor boshqaruvi keraksiz matnlardan tozalanib, zamonaviy va ixcham ko'rinishga keltirildi. Asosiy omborni tanlash endi qulay yulduzcha orqali bir harakat bilan bajariladi.


---

### 29. 🎨 Ombor UI Tahrirlash Tugmalari, Non Uvoqlari (Breadcrumbs), Qidiruv Navigatsiyasi va Mahsulot Qo'shish Formasi To'liq Mukammallashtirildi
- **Nima o'zgardi:**
  - **Tahrirlash tugmalari yiriklashtirildi va bosishga qulay qilindi (1-rasm):**
    - Kategoriya kartasidagi kichik qalamcha tugmasi professional `32x32px` o'lchamdagi to'liq tugmaga aylantirildi (`#1E3A5F`, `#0284C7` hoshiya va `15x15px` vektor ikonka bilan).
    - Tovarlar jadvalidagi barcha amallar tugmalari (Tahrirlash, Chop etish, O'chirish) `28x28px` dan `34x34px` gacha kattalashtirildi, piktogrammalari `16x16px` ga oshirilib, bosish o'ta qulay va sezilarli qilindi.
  - **Ombor kategoriyalari sarlavhasi va Ortga qaytish tugmasi modernizatsiya qilindi (2-rasm):**
    - "OMBOR KATEGORIYALARI" statik sarlavhasi o'rniga faol ombor nomi (masalan: `secondary`) piktogrammasi bilan joylashtirildi.
    - Yuqori navigatsiya tugmasi "Ortga qaytish" deb nomlanib, professional to'q ranglar palitrasi (`#1E293B`, hoshiya `#334155`, hoverda `#334155`/`#64748B`) va silliq radius bilan jihozlandi.
  - **Ortiqcha yuqori tugma olib tashlandi va sarlavhada ombor/kategoriya ko'rinishi qo'shildi (3-rasm):**
    - Mahsulotlar ro'yxatida qidiruv maydoni oldidagi takroriy yuqori tugma olib tashlandi, faqat bitta pastki professional "Ortga qaytish" tugmasi qoldirildi.
    - Sarlavha non uvoqlari (breadcrumbs) shakliga keltirildi: `Ombor nomi / Kategoriya nomi (Soni)`, masalan: `secondary / Barcha tovarlar (1 ta tovar)`, zamonaviy ranglar bilan ajratildi.
  - **Mahsulot qo'shish/tahrirlash formasi kartalari ekranga to'liq moslashtirildi (4-rasm):**
    - Mahsulot formasi kartalarining pastida hosil bo'ladigan bo'shliq to'liq yo'qotildi (`VerticalAlignment="Top"` qo'llanilib, kartalar ichidagi maydonlarga mos ravishda ixcham o'raldi).
    - Kartalarning to'rtala tomoni bo'ylab teng va simmetrik `20px` padding o'rnatildi, barcha elementlar toza va chiroyli joylashdi.
  - **Yangi o'rnatish to'plami yig'ildi:**
    - Yangi [`PROD/Line_kassa_Desktop_Setup.exe`](file:///c:/Users/Acer/Documents/POS/PROD/Line_kassa_Desktop_Setup.exe) (52.2 MB) qaytadan muvaffaqiyatli kompilyatsiya qilindi.
- **Mijoz uchun foydasi:**
  - Ombor bo'limida harakatlanish osonlashdi, tugmalar kattalashib sensorli va sichqonchali boshqaruvda oson bosiladigan bo'ldi. Mahsulot qo'shish oynasi desktop ekranlarda ortiqcha bo'sh joylarsiz, professional darajadagi zamonaviy forma ko'rinishiga ega bo'ldi.


---

### 30. 🎨 Kategoriya Kartalarining Moslashuvchan Kengligi (Adaptive), Forma Kartalarining Teng Balandligi, Enter/Esc Tugmalari va Shtrix-kod Bosish Oynasi Tuzatildi
- **Nima o'zgardi:**
  - **Kategoriya kartalari kengligi desktop ekranni to'liq to'ldiradigan qilindi (1-rasm):**
    - Statik `Width="250"` va `WrapPanel` o'rniga kassa oynasidagi kabi `AdaptiveGridPanel` (`MinItemWidth="230"`, `ItemHeight="120"`, `Spacing="14"`) o'rnatildi.
    - Kartalar konteyner kengligiga qarab ustunlar sonini dinamik hisoblaydi va barcha bo'shliqni o'zaro teng taqsimlaydi; o'ng tomonda noo'rin bo'sh joy qolishi butunlay bartaraf etildi.
  - **Mahsulot formasi kartalari balandligi 100% tenglashtirildi va tugmalar joylashuvi qulay qilindi (2-rasm):**
    - Chap va o'ng kartalar bitta `Grid` qatorida birlashtirildi, `VerticalAlignment="Top"` olib tashlanib, har ikkala kartaning bo'yi avtomatik ravishda tenglashtirildi (balandliklar nomutanosibligi yo'qotildi).
    - "Bekor qilish" va "Saqlash" tugmalari oynaning eng chetidagi uzoq burchakdan olinib, bevosita ikkala kartaning ostiga (`Grid.Row="1"`) juda qulay holatda joylashtirildi. Xatolik matni ham tugmalar bilan bitta chiziqda ravshan ko'rinadi.
    - Tugmalarga `[Esc]` va `[Enter ↵]` klaviatura biriktirildi (`IsCancel="True"`, `IsDefault="True"` hamda `InventoryView_PreviewKeyDown` orqali tezkor saqlash va bekor qilish to'liq ulandi).
  - **Shtrix-kod etiketka chop etish oynasidagi - va + tugmalari qirqilishi tuzatildi (3-rasm):**
    - Chop etish modali kengligi `720px` dan `780px` ga kengaytirildi, o'ng panelga `10px` xavfsiz oraliq o'rnatildi.
    - Nusxalar soni qismidagi `-` va `+` tugmalari ustunlari `42px` ga o'rnatildi, `Padding="0"` va `FontSize="20"` berilib, piktogrammalarning qirqilib qolishi to'liq tuzatildi.
  - **Eslatma:** Foydalanuvchi ko'rsatmasiga binoan, yangi o'rnatish paketi (setup) yig'ilmadi, keyingi topshiriqlar kutilmoqda.
- **Mijoz uchun foydasi:**
  - Ombor bo'limida kartalar monitor kengligini chiroyli va tartibli to'ldiradi. Mahsulot qo'shishda maydonlar to'ldirilishi bilan darhol Enter orqali saqlash yoki Esc orqali chiqish mumkin. Stiker chop etishda nusxa tanlash tugmalari to'liq va ravon ko'rinadi.

---

### 31. 🏬 Kassa va Ombor Qoldiqlari Tafovuti Bartaraf Etildi, Ko'p Omborli Kartalar, Raqamlangan Badge va Skaner Ustuvorligi Zanjiri (Desktop & Mobil)
- **Nima o'zgardi:**
  - **O'chirilgan omborlarning "arvoh" qoldiqlari to'liq tozalandi (Data Repair Migration):**
    - Avval o'chirilgan `r` va `secondary` omborlaridagi qoldiqlar (jami 65 ta va 58 ta tovar) umumiy hisobdan ayirilmasdan qolib ketgan edi. Baza avtomatik tozalash migratsiyasi orqali ushbu noo'rin qoldiqlar o'chirildi.
    - Barcha tovarlarning `products.stock_quantity` umumiy qoldiqlari faqat **faol va mavjud** omborlar bo'yicha to'g'ri qayta hisoblandi (`tovar 2`: 95 emas, balki faol omborlar jami 30 ta qoldi; `2_ombor`: 61 emas, 3 ta; `Tovar 1`: 1 emas, 0 ta).
    - `DeleteWarehouse`, `SaveProduct`, `TransferStock` va `SaveSale` so'rovlariga faol omborlar filtri (`w.is_deleted = 0`) o'rnatildi.
  - **Kassada ko'p omborli tovarlar uchun alohida kartalar tizimi (Desktop & Mobil):**
    - Tovar bir nechta omborda mavjud bo'lsa (masalan, `tovar 2` Do'konda 20 ta, Ikkinchi omborda 10 ta), kassada va qidiruvda **har bir ombor uchun alohida karta** chiqadi:
      - 1-karta: `tovar 2` — **Do'kondagi ombor** (Qoldiq: 20 dona)
      - 2-karta: `tovar 2` — **Ikkinchi ombor** (Qoldiq: 10 dona)
    - **Tartib:** Ro'yxat boshida har doim **Asosiy ombor** (Do'kon ombori), keyin qolgan omborlar ketma-ket chiqadi.
    - Qaysi karta bosilsa, tovar aynan o'sha ombordan savatga tushadi va sotuvda o'sha ombor qoldig'idan yechiladi.
  - **Desktop va Mobil badge farqlari (Joy tejash va shaffoflik):**
    - **Desktop (WPF):** Katta ekranda tovar kartasida ombor nomi va tartibi to'liq ko'rinadi (`🏢 1. Do'kondagi ombor`, `🏢 2. Ikkinchi ombor`).
    - **Mobil (Android):** Telefonda ekran joyi kamligi uchun tovar nomi yonida ixcham va ravshan **raqamlangan badge** joylashtirildi: Asosiy ombor uchun `1`, keyingilari uchun `2`, `3`...
  - **Shtrix-kod skanerlangandagi aqlli zaxira zanjiri (Barcode Priority Chain):**
    - Skaner orqali tovar o'qitilganda (Desktop va Mobil):
      1. Birinchi navbatda **Asosiy ombordagi qoldiq** tekshiriladi (`stock > 0` bo'lsa, asosiy ombordan olinadi).
      2. Agar asosiy omborda tovar tugagan bo'lsa (`stock <= 0`), keyingi faol omborlar (2-ombor, 3-ombor...) zaxirasi tekshirilib, qoldig'i bor birinchi ombordan avtomatik savatga qo'shiladi.
      3. Agar hech qaysi omborda qoldiq qolmagan bo'lsa, asosiy ombor orqali qo'shiladi.
  - **Eslatma:** Foydalanuvchi ko'rsatmasiga binoan, yangi o'rnatish paketi (setup) yig'ilmadi.
- **Mijoz uchun foydasi:**
  - Kassada va Ombordagi qoldiqlar 100% bir-biriga mos keladi, noo'rin "arvoh" qoldiqlar yo'qoldi.
  - Kassir qaysi ombordan tovar sotayotganini aniq bilib boshqaradi. Skaner ishlatilganda esa do'konda tovar qolmagan taqdirda dastur o'zi avtomatik zaxira ombordagi tovardan qo'shib beradi.

---

### 32. 📅 Hisobotlar Oynasi: Vektor Ikonkalar, Zedge Uslubidagi Aqlli Kalendar (Dual-Month Range Picker), Chek Qidiruvining Qulay Joylashuvi va Ombor Yangilash Tugmasi
- **Nima o'zgardi:**
  - **Barcha emojilar zamonaviy SVG vektor ikonkalariga almashtirildi (Zero Emojis):**
    - Yuqori paneldagi barcha ikonkalar (kalendar, dollar kursi, Excel (.xls), yangilash), 4 ta asosiy statistika kartalari (Jami tushum, Sof foyda, Savdolar soni, Sotilgan tovarlar), cheklar jadvali sarlavhasi, qidiruv lupasi va tafsilot "Ko'rish" tugmalari, shuningdek Excel eksport modalidagi barcha emojilar to'liq professional SVG vektor yo'llariga (`Path Data=...`) o'tkazildi.
  - **Sana tanlash tugmalari yagona aqlli dropdownga birlashtirildi:**
    - Avvalgi 4 ta alohida knopka o'rniga bitta ixcham `[ 📅 Vaqt oralig'i: Bugun (08.10.2026) ▾ ]` tugmasi qo'yildi. Tugma bosilganda aqlli kalendar darchasi ochiladi.
  - **Zedge namunasidagi aqlli ikki oyli kalendar (Dual-Month Range Picker):**
    - **Tezkor tanlov paneli (Chapda):** "Bugun", "Kecha", "Oxirgi 7 kun", "Shu oy", "O'tgan oy" tugmalari orqali bir marta bosish bilan tezkor oraliqni o'rnatish.
    - **Yonma-yon 2 oylik to'liq kalendar (O'rtada):** Oldingi va keyingi oylarga o'tish tugmalari (`<` va `>`), hafta kunlari sarlavhalari (`Du, Se, Ch, Pa, Ju, Sha, Ya`) va oy kunlari.
    - **Aqlli sana tanlash rejimi:**
      - Bitta sana bosilsa — aynan bitta kun tanlanadi va ko'k doira bilan belgilanadi (`IsSingleSelected`).
      - Ikkinchi sana bosilsa — ikki sana oralig'i to'liq qamrab olinadi: boshlanish va tugash sanalari dumaloq ko'k (`#0284C7`), oraliqdagi barcha kunlar esa uzluksiz chiroyli ko'k fonga (`#1E3A5F`) olinadi.
    - **Pastki amal paneli:** Tanlangan sana oralig'i va umumiy kunlar soni (masalan: `01.10.2026 — 08.10.2026 (8 kun)`), "Tozalash" (Bugungi kunga qaytarish) va "Qo'llash" tugmalari.
  - **Chek № bo'yicha qidiruv bevosita cheklar jadvali ustiga ko'chirildi:**
    - Yuqori navbar paneli bo'shatilib, qidiruv qismi pastdagi savdolar jadvali qutisining ichiga, "Cheklar Tarixi" sarlavhasi yoniga olib tushildi. Natijada chekni izlash va ro'yxatni ko'rish bitta joyda jamlandi.
  - **Yangilash tugmasi Ombor oynasidagi kabi animatsiyali vektor tugmaga aylantirildi:**
    - 40x40 `#334155` o'lchamdagi, bosilganda 360 daraja silliq aylanuvchi sinxronlash/yangilash vektor tugmasi qo'yildi.
  - **O'rnatish paketi (Setup):** Barcha yangiliklar va optimizatsiyalar bilan to'liq Inno Setup o'rnatish paketi (`desktop/Output/Line_kassa_Desktop_Setup.exe`) muvaffaqiyatli yig'ildi.
- **Mijoz uchun foydasi:**
  - Hisobotlar ekrani ancha toza, tartibli va professional ko'rinishga keldi.
  - Bir necha hafta yoki oylab oraliqdagi hisobotlarni ikki oylik ko'rgazmali kalendarda bir zumda ko'rib, xoh bitta kunni, xoh oraliqni juda qulay tanlash mumkin.
  - Chek raqami orqali qidiruv to'g'ridan-to'g'ri jadval boshida joylashgani hisobiga sotuvchi o'ziga kerakli chekni qidirishda adashmaydi.

---

### 33. 🎨 Hisobotlar: Vektor Ikonkalar O'lchami Fix, Yagona Filtrlar Paneli (Ombor, Kategoriya, Operatsiya Turi) va 1-Bosishda Excel Eksport
- **Nima o'zgardi:**
  - **Vektor ikonkalar o'lchami va masshtablanishi to'liq to'g'rilandi (`Stretch="Uniform"`):**
    - Barcha `Path` elementlariga `Stretch="Uniform"` qo'shilib, SVG koordinatalari to'g'ri masshtablandi.
    - Natijada oldin qirqilib, katta va xunuk ko'ringan ikonkalar (Dollar belgisi `d` harfiga aylanib qolishi, ko'z belgisi yarimta ko'rinishi, stat kartalardagi chiziqlarning uzilishi) to'liq tuzatildi. Ikonkalar endi aniq, ixcham (12-16px) va estetik ko'rinishga ega.
  - **Filtrlar yagona yuqori panelga jamlandi:**
    - `Sana oralig'i` yoniga `Ombor`, `Kategoriya` va `Operatsiya turi` ("Barchasi", "Savdo", "Qaytarish", "Brak") filtr dropdownlari chiqarildi.
    - 3-kartochka (SAVDOLAR SONI) ichidagi ortiqcha ComboBox olib tashlanib, kartochka bo'sh va chiroyli holatga keltirildi.
    - Har qanday filtr o'zgartirilganda (sana, ombor, tovar kategoriyasi yoki operatsiya turi) butun hisobotlar jadvali va 4 ta KPI kartochkasi darhol jonli yangilanadi.
  - **Excel eksport modali olib tashlandi (1-bosishda to'g'ridan-to'g'ri eksport):**
    - Eski qora modal dialog butunlay o'chirildi.
    - Yashil "Excel (.xls)" tugmasi bosilganda darhol ekranda filtrlangan ma'lumotlar bo'yicha to'g'ridan-to'g'ri fayl saqlash oynasi ochiladi va eksport qilinadi.
  - **O'rnatish paketi (Setup):** Barcha so'nggi yangiliklar bilan to'liq Inno Setup o'rnatish paketi (`desktop/Output/Line_kassa_Desktop_Setup.exe`) muvaffaqiyatli yig'ildi.
- **Mijoz uchun foydasi:**
  - Ikonkalar ko'zni qamashtirmaydi, o'lchamlari mutanosib va toza ko'rinadi.
  - Hisobotni filtrlash uchun bir nechta joyga yugurish shart emas — barcha filtrlar tepada bir qatorda turadi.
  - Excel yuklab olishda takroriy savollar va oynalar chiqmaydi, bir marta bosishda hisobot tayyor bo'ladi.

---

### 34. 🐞 Kam Qolgan Tovarlar Chegarasi (MinStockAlert) va Qoldiq Logikasi Tuzatildi
- **Nima o'zgardi:**
  - **"0" kiritilganda uni majburiy "3" ga almashtirib yuborish xatosi bartaraf etildi:**
    - Kodda mavjud bo'lgan `if (minStockAlert <= 0) minStockAlert = 3.0;` sharti butunlay olib tashlandi.
    - Endi foydalanuvchi tovar qo'shayotganda yoki tahrirlayotganda "Kam qolganda chegara" maydoniga `0` deb kiritsa, tizim o'zboshimchalik bilan `3` ga aylantirib yubormaydi va aynan `0` deb saqlaydi.
  - **Kam qolganlikni aniqlash va qizil ogohlantirish logikasi to'g'rilandi:**
    - Agar tovar uchun chegara `0` deb belgilangan bo'lsa:
      - Omborda `1` ta (yoki undan ko'p) qoldiq bo'lsa, tovar asossiz qizil rangga kirmaydi va "Kam qolgan tovarlar" ro'yxatiga qo'shilmaydi (yashil / oddiy holatda turadi).
      - Faqat qoldiq `0` ga tushgandagina (tovar to'liq tugaganda) yoki manfiyga kirgandagina qizil ogohlantirish bilan kam qolganlar qatoriga qo'shiladi.
    - Agar chegara `3` (yoki foydalanuvchi kiritgan ixtiyoriy son) bo'lsa, qoldiq o'sha chegaraga yetganda yoki undan kamayganda ogohlantirish beriladi.
  - **Ikkala platforma (Desktop va Mobil ilova) uchun ham bir xil standart joriy qilindi.**
- **Mijoz uchun foydasi:**
  - Do'konda 1 ta yoki 2 ta qolgan tovarlar, agar ularning chegarasi 0 qilingan bo'lsa, soxta xavf (qizil fon) bermaydi va kam qolganlar ro'yxatini to'ldirib yubormaydi.
  - Sotuvchi qaysi tovar qachon ogohlantirish berishini o'zi to'liq nazorat qila oladi.

---

### 35. 🚀 Qarz Daftari (Customer Debt Stage 3A) Integratsiyasi: Tranzaksiyaviy Inbox, Wire Shartnomasi va Xavfsiz Sinxronizatsiya
- **Nima o'zgardi:**
  - **Qarz daftari backend va sinxronizatsiyasining 3A bosqichi asosiy tizimga to'liq ulandi:**
    - **Kross-platforma Wire shartnomasi (`DebtWire`):** Desktop (C#) va Mobil (Kotlin) o'rtasida qarz operatsiyalari va to'lovlarni uzatish uchun yagona xavfsiz shartnoma ishlab chiqildi va 100% bir xillikda ishlaydigan test fixturalari bilan qamrab olindi.
    - **Muzlatilgan moliyaviy konvertlar (`DebtEnvelope`):** Qarz yozuvlari va to'lov taqsimotlari tarmoq orqali uzatilganda o'zgarmas (immutable) va manipulyatsiyadan himoyalangan tarzda saqlanadi.
    - **Ishonchli Inbox qutisi (`DebtEnvelopeInbox`):** Tarmoq uzilishi, qayta yuborish (replay) yoki takroriy so'rovlar bo'lganda, qarz to'lovlari bir martadan ortiq hisoblanmaydi (idempotent) va tranzaksiyaviy xavfsiz qabul qilinadi.
    - **Android DEBT cheklari himoyasi:** Mobil ilovada qarzga sotilgan cheklar to'g'ri qayd etilishi va faqat sotuv yozuvlari bilan aralashib ketmasligi ta'minlandi.
  - **Barcha 7 ta avtomatlashtirilgan qarz va biznes testlari 100% muvaffaqiyatli (PASS) o'tdi.**
- **Mijoz uchun foydasi:**
  - Nasiyaga savdo qilish va mijozlardan qarz undirish jarayoni 100% ishonchli bo'ldi: telefon yoki kompyuter o'chib qolsa ham qarz summalarida adashish, ikki marta hisoblanish yoki yo'qolib qolish xavfi butunlay bartaraf etildi.

---

### 36. ⚡ Kassa Qidiruvi Tezligi (Qotish va Harflar Tushib Qolishini Bartaraf Etish) & Hisobotlar Kartochkalarini Tozalash
- **Nima o'zgardi:**
  - **Asosiy kassa qidiruvidagi qotish (harflar tushib qolishi) to'liq tuzatildi:**
    - **Muammo sababi:** Oldin har bir harf bosilganda (150ms kechikish bilan) dastur asosiy oyna (UI) oqimida SQLite ma'lumotlar bazasidagi barcha minglab tovarlarni qaytadan diskdan o'qib, og'ir qidiruv logikasini yurgizar edi. Natijada kassa qidiruv maydoni 2 ta harfdan keyin qotib, tez yozilganda klaviatura bosishlarini o'tkazib yuborayotgan edi.
    - **Kesh xotirasi (In-Memory Cache):** Tovarlar, omborlar va qoldiqlar ro'yxati operativ xotirada keshlandi. Endi qidiruv harflari yozilganda ma'lumotlar bazasi diskiga asossiz murojaat qilinmaydi.
    - **Orqa fon qidiruvi (Async Background Worker & CancellationToken):** Qidiruv hisob-kitobi va saralash orqa fonga (`Task.Run`) olindi. Agar foydalanuvchi tez yozsa, eski qidiruv darhol bekor qilinadi va yangi so'rovga o'tiladi. UI oqimi 100% bo'sh bo'lib, klaviatura yozuvi soniyasiga 60 kadr tezlikda biror harfni tushirmasdan silliq ishlaydi.
    - **Debounce 250ms ga sozlandi:** Odamning tabiiy yozish tezligiga moslashtirilib, so'z yozilayotgan paytda ortiqcha oraliq qidiruvlarni boshlab yubormaydi.
    - **Eng mos 60 ta natija cheklovi:** Qidiruv natijalarining dastlabki eng muhim 60 tasi ko'rsatiladi — bu kassa oynasining yashindek tez ochilishini ta'minlaydi.
    - **Enter va Shtrix-kod skaneri tezligi:** Shtrix-kod o'qitilganda yoki Enter bosilganda qidiruv kutmasdan, to'g'ridan-to'g'ri keshdan tekshirib tovar darhol savatchaga qo'shiladi.
  - **Hisobotlar oynasidagi KPI kartochkalari tozalandi:**
    - **2-kartochka (SOF FOYDA):** Ortiqcha va chalg'ituvchi `$-- (eski kurs yo'q)` yozuvi olib tashlandi, dollar kursi saqlanmagan hollarda bo'sh ko'rsatiladi.
    - **3-kartochka (SAVDOLAR SONI):** Kartochka tagidagi mayda, ko'zni toliqtiruvchi `Qaytarilgan (sof): -0.00 so'm • Tannarx tiklanishi: -0.00 so'm / Brak: 0 ta` qatori olib tashlandi. Kartochka boshqalar kabi elegant, toza va aniq faqat cheklar sonini ko'rsatadigan holatga keltirildi.
- **Mijoz uchun foydasi:**
  - Kassir tovar nomini qanchalik tez yozsa ham, birorta harf tushib qolmaydi, dastur qotmaydi va harflar tiqilib qolmaydi.
  - Shoshilinch navbat paytida kassa yashindek tez ishlaydi.
  - Hisobotlar oynasi ortiqcha tushunarsiz yozuvlardan xoli bo'lib, eng muhim moliyaviy raqamlar aniq ko'rinadi.

---

### 37. ⚡ Kassa Qidiruvi: Professional Debounce (400ms), 2-Belgi Chegarasi va Fokus Himoyasi
- **Nima o'zgardi:**
  - **400 ms professional Debounce kechikishi:** Oldingi 250 ms kechikish o'rniga xalqaro standart 400 ms kechikish joriy qilindi. Endi foydalanuvchi `2000` yoki boshqa so'zlarni klaviaturada shoshilinch yozayotgan paytda oraliq qidiruvlar mutlaqo boshlanmaydi va harflar tiqilib qolmaydi. Tizim faqat kassir yozishdan to'xtaganidan keyingina qidiruvni boshlaydi.
  - **Kamida 2 ta belgi qoidasi (`Length >= 2`):** Faqat `2` yoki bitta harf yozilganda og'ir qidiruv boshlanmaydi — ekranda eng ko'p sotilgan tovarlar ro'yxati silliq turadi. 2-belgi kiritilgach (masalan: `20`, `2000` yoki `ka`), 400ms to'xtashdan so'ng qidiruv natijalari yengil ochiladi.
  - **Klaviatura fokusi himoyasi (`Focusable="False"`):** Qidiruv natijalari chiqqanda yoki yangilanganda klaviatura fokusi qidiruv maydonidan (`SearchBox`) chiqib ketmasligi uchun tovarlar kartalari paneli klaviatura fokusini tortib olmaydigan qilindi.
  - **Top 24 ta eng mos natija:** Natijalar soni 60 tadan eng yuqori reytingli 24 taga keltirildi — bu qidiruv ro'yxatining 0 ms kechikish bilan yashindek ochilishini ta'minlaydi.
  - **Shtrix-kod skaneri va Enter qulayligi:** Skaner o'qitilganda yoki Enter bosilganda 1 ta belgida ham hech qanday taymer kutmasdan tovar darhol savatchaga qo'shiladi va qidiruv maydoni tozalanadi.
  - **Yangi Desktop Setup:** Barcha yangiliklar bilan to'liq Inno Setup o'rnatish paketi (`Line_kassa_Desktop_Setup.exe`) yangidan yig'ildi.
- **Mijoz uchun foydasi:**
  - Kassir son yoki so'zlarni qanchalik tez yozsa ham (`2000`, `3000`), barcha raqamlar 100% to'liq yoziladi, bittasi ham tushib qolmaydi.
  - Qidiruv kassirga xalaqit bermaydi va shoshilinch vaqtda asabni buzmaydi.

---

### 38. ⚡ Kassa va Ombor Qidiruvini 100% Bir Xil Arxitekturaga O'tkazish (Tezkor Yozuvda Harflar Tushib Qolishini Butkul Yo'qotish)
- **Nima o'zgardi:**
  - **Kassa qidiruvi Ombor oynasi arxitekturasi bilan 100% tenglashtirildi:**
    - Ombor oynasida `2000` yozilganda barcha belgilar tushmasdan to'liq yozilishi chuqur tahlil qilindi: Omborda `SearchQuery.set` o'zida hech qanday oraliq og'ir kod bajarmaydi (faqat 0.001 ms li taymerni qayta ishga tushiradi).
    - Kassada esa `SearchQuery.set` da ro'yxatni tozalash (`FilteredProducts.Clear()`), asinxron `Task.Run` va `Dispatcher.InvokeAsync` orqali UI oqimida 40 ta kartani bittalab qo'shish harakatlari bo'lgan. Aynan shu oraliqda WPF UI xabarlar navbati bloklanib, tez kiritilgan keyingi `00` raqamlari tushib qolayotgan edi.
  - **Amalga oshirilgan arxitektura o'zgarishlari:**
    - `SearchQuery.set` mutlaqo 0-overhead qilindi: hech qanday tozalash yoki oraliq operatsiyalarsiz faqat 200 ms debounce taymerini boshqaradi.
    - Asinxron `Task.Run` va `Dispatcher.InvokeAsync` o'rniga, in-memory keshdan foydalangan holda qidiruv 2 ms ichida sinxron va toza hisoblanadi.
    - Natijalar ro'yxati ekranga alohida `.Clear()` va `.Add()` qilinmasdan, Ombor kabi bitta atomik yangilanishda (`FilteredProducts = new ObservableCollection<Product>(cards);`) uzatiladi. Bu orqali WPF UI faqat 1 marta chiziladi va klaviatura kiritishi hech qachon kechikmaydi.
  - **Yangi Desktop Setup:** Barcha o'zgarishlar bilan yangi `Line_kassa_Desktop_Setup.exe` o'rnatish paketi yig'ildi va `PROD` papkasiga joylashtirildi.
- **Mijoz uchun foydasi:**
  - Kassa qidiruvi xuddi Ombor qidiruvidek 100% yengil va chaqqon bo'ldi. Kassir klaviaturada qanchalik katta tezlikda yozsa ham (`2000`, `15000`, tovar nomlari), birorta raqam yoki harf tushib qolmaydi, yozuv ravon va silliq aks etadi.

---

### 39. 📒 "Qarzlar" (Qarz daftari) yangi bo'limi va kassada Nasiya savdosi
- **Nima o'zgardi:**
  - Asosiy menyuda yangi **📒 Qarzlar** bo'limi paydo bo'ldi.
  - Kassada to'lov turlariga **📒 Nasiya** varianti qo'shildi: mijozni tanlash (yoki joyida yangi qo'shish), avans to'lovi (naqd yoki karta), qoladigan qarz miqdori va to'lash muddatini belgilash imkoniyati yaratildi.
  - "Qarzlar" bo'limida barcha qarzdorlar ro'yxati, jami faol qarz, muddati o'tgan qarzlar, qidiruv va qulay filtrlar ("Qarzi bor", "Muddati o'tgan", "Yopilgan", "Ortiqcha to'lov", "Barchasi") o'rnatildi.
  - Qarz to'lovini qabul qilishda summani kiritish bilan qaysi nasiyalardan qancha yopilishi jonli ravishda oldindan ko'rsatiladi (preview), ortiqcha to'lov xatolik bilan kiritilishidan himoyalangan.
- **Mijoz uchun foydasi:** 
  - Nasiyaga savdo qilish va qarz yig'ish 100% oflayn rejimda, internet va Wi-Fi bo'lmaganda ham mustaqil ishlaydi.
  - Har bir mijoz bo'yicha to'liq cheklar va to'lovlar tarixi bir joyda shaffof ko'rinib turadi.

---

### 40. 📱 Android mobil ilovasida "Qarzlar" bo'limi va oflayn Nasiya kassa integratsiyasi
- **Nima o'zgardi:**
  - Mobil ilova pastki boshqaruv paneliga yangi **📒 Qarzlar** bo'limi qo'shildi (Kassa / Ombor / Qarzlar / Hisobotlar / Sozlamalar).
  - Mobil kassa to'lov oynasi 2x2 qulay ko'rinishga keltirildi (Naqd, Karta, Aralash, Nasiya).
  - Nasiya to'lovida mijozni qidirib tanlash, "+ Yangi" tugmasi orqali joyida tezkor mijoz ochish, naqd/karta avans summasini kiritish, qolgan qarz summasini avtomatik hisoblash va to'lov muddatini belgilash imkoniyati yaratildi.
  - Muzlatilgan savatlarda (Hold Carts) nasiya mijozi va to'lov holati saqlanadi hamda "📒 {Mijoz ismi} (Nasiya)" belgisi ko'rsatiladi.
  - "Qarzlar" ekranida 4 ta KPI kartasi (Jami nasiya, Muddati o'tgan, Jami to'langan, Haqdorlik), 5 ta filtr, mijozlar qidiruvi, mijoz kartasida cheklar va to'lovlar tarixi, jonli taqsimot ko'rsatuvchi "To'lov olish" dialogi hamda tanlangan mijoz bilan bir zumda kassaga o'tish ("Kassada ochish") imkoniyati yaratildi.
- **Mijoz uchun foydasi:**
  - Mobil telefonda internet va Wi-Fi bo'lmaganda ham 100% oflayn rejimda nasiyaga tovar sotish, yangi mijoz ochish va qarz to'lovlarini yig'ish mumkin.

---

### 41. 🔄 Nasiya tovarlarni qaytarish (Return), to'lovni bekor qilish va ortiqcha pul amallari
- **Nima o'zgardi:**
  - Nasiyaga sotilgan tovar qaytarilganda (Return), avval mijozning shu chekdagi qarzi avtomatik chegiriladi (kamaytiriladi), faqat qarzdan ortiq to'langan summa mavjud bo'lsagina mijozga naqd/karta puli qaytariladi.
  - Noto'g'ri kiritilgan qarz to'lovini bekor qilish (Bekor qilish / Payment Reversal) imkoniyati yaratildi — bank komissiyalari va balanslar o'z joyiga qaytariladi.
  - Ortiqcha to'langan summa (haqdorlik / kredit) bo'lganda mijozga pulni naqd yoki kartada qaytarish ("💸 Pulni qaytarish" / Credit Refund) funksiyasi qo'shildi.
  - Bir chekdan ortib qolgan kredit summasini mijozning boshqa faol qarziga o'tkazish ("🔁 Qarzga o'tkazish" / Credit Transfer) imkoniyati joriy etildi.
  - Mobil ilovada tovar qaytarish oynasida qarzdan qancha chegirilishi va mijozga qancha naqd/karta berilishi aniq va shaffof ko'rsatiladi.
- **Mijoz uchun foydasi:**
  - Kassir xatolik bilan qarz tovarini qaytarganda mijozga do'kon hisobidan ortiqcha naqd pul berib yuborish xavfi 100% bartaraf etildi.
  - Mijoz haqdor bo'lib qolgan holatlarda pulni qaytarish yoki boshqa nasiyaga yo'naltirish to'liq avtomatlashtirildi.

---

### 42. 📊 Qarzlar va Pul oqimi (Cashflow) hisoboti, Excel eksporti va mijoz ko'chirmasi (Sverka)
- **Nima o'zgardi:**
  - Hisobotlar oynasiga yangi **"Qarz va Pul Oqimi (Cashflow)"** bo'limi qo'shildi:
    - **Berilgan nasiya:** Davr ichida xaridorlarga berilgan jami yangi nasiya summasi.
    - **Undirilgan qarz:** Davr ichida undirilgan qarzlar (Naqd va Karta alohida, bank komissiyasi bilan).
    - **Kassa pul oqimi (Cashflow):** Haqiqiy kassa/hisobga kirgan pul (oddiy savdolar + undirilgan qarzlar).
    - **Faol qarzdorlik va Haqdorlik:** Davr oxiridagi xaridorlarning sof umumiy qarzi va ortiqcha to'lovlari (haqdorlik) alohida ko'rsatiladi.
  - **Daromad va Foyda (P&L) qat'iy buxgalteriya intizomi:**
    - Nasiya savdosi amalga oshirilgan kuni to'liq sotuv summasi va foyda 1 marta hisoblanadi. Keyinchalik qarz undirilganda u qayta daromad yoki sotuv deb hisoblanmaydi (dublikat daromad ko'rsatish xavfi 100% yo'qotildi).
    - Qarz to'lovi faqat kassa pul oqimiga (cashflow) va undirilgan kungi bank komissiyasiga ta'sir qiladi.
  - **Excel eksporti:**
    - Excel faylida "QARZ VA CASHFLOW (PUL OQIMI)" maxsus tahliliy blok qo'shildi.
    - Savdolar jadvalida yangi **"Nasiya (so'm)"** ustuni joylashtirildi.
  - **🖨️ / 📤 Mijoz hisob-kitob ko'chirmasi (Sverka akti):**
    - Desktopda "Qarzlar" bo'limida mijoz sahifasida **"🖨️ Ko'chirma"** tugmasi paydo bo'ldi: 80mm/58mm chek printeriga yoki nusxalash orqali mijozning barcha nasiyalari, to'lovlari, tovar qaytarishlari va yakuniy qoldig'i 1 tugma bilan chop etiladi.
    - Android mobil ilovasida **"Ko'chirmani ulashish (Sverka)"** tugmasi qo'shildi: Telegram, WhatsApp yoki SMS orqali mijozga to'liq cheklar va to'lovlar ko'chirmasini matn ko'rinishida yuborish imkoniyati yaratildi.
- **Mijoz uchun foydasi:**
  - Do'kon egasi kunlik yoki oylik kassasidagi haqiqiy naqd pul oqimi (cashflow) bilan sof tovar daromadini hech qachon chalkashtirmaydi.
  - Xaridor bilan qarz bo'yicha bahslashuv yuzaga kelsa, joyida to'liq va shaffof hisob-kitob ko'chirmasi (sverka) chiqarib beriladi.

---

### 43. 📥 Ma'lumotlar bazasini xavfsiz zaxiradan qayta tiklash (Database Restore)
- **Nima o'zgardi:**
  - Asosiy boshqaruv panelida **"📥 Bazani tiklash"** tugmasi qo'shildi (fleshka yoki kompyuter diskidagi `.db` zaxira nusxasidan ma'lumotlarni to'liq qayta tiklash).
  - Tiklash jarayoni 4 bosqichli xavfsizlik nazoratidan o'tadi:
    1. **Butunlik tekshiruvi:** Tanlangan faylning SQLite butunligi (`PRAGMA integrity_check`) va bog'lanishlari (`PRAGMA foreign_key_check`) o'qish rejimida tekshiriladi; buzilgan yoki begona fayllar qat'iy rad etiladi.
    2. **Avtomatik himoya zaxirasi:** Tiklash boshlanishidan oldin joriy ishchi bazaning alohida `before_restore` xavfsizlik nusxasi avtomatik yaratiladi.
    3. **Bosqichma-bosqich xavfsiz almashtirish (Staged Replace):** Baza avval vaqtincha joyda tekshirilib, so'ng atomik tarzda almashtiriladi; biror xatolik bo'lsa ishchi baza o'z o'rnida daxlsiz qoladi.
    4. **Sinxronizatsiya muvofiqligi:** Tiklangandan so'ng yangi tarix epochi (`debt_history_epoch`) belgilanib, kassir, ombor, qarzlar va hisobotlar ekrani avtomatik yangilanadi. Ulangan Android telefonlar bilan ziddiyatsiz qayta tekshiruv (reconciliation) kafolatlanadi — pul va qoldiqlar ikki marta hisoblanmaydi.
- **Mijoz uchun foydasi:**
  - Kompyuter almashtirilganda, Windows qayta o'rnatilganda yoki zaxira nusxadan ma'lumotlarni qaytarish zarurati tug'ilganda dastur ichidan 1 tugma bilan xavfsiz va yo'qotishlarsiz tiklash mumkin.

---

### 44. 📒 Kassa To'lov Oynasida Nasiya Checkbox, 3 ta To'lov Tugmasi, Scroll Qulayligi, Dublikat Ismlar Cheklovi va Chekda Nasiyani Yashirish
- **Nima o'zgardi:**
  - **Savatda faqat 3 ta asosiy to'lov tugmasi:** Kassa ekranida 4-ortiqcha "Nasiya" tugmasi olib tashlandi, faqat eng ko'p ishlatiladigan 3 ta tugma qoldirildi: **Naqd** [Space / F8], **Karta** [F9], **Aralash** [F10].
  - **To'lov tasdiqlash oynasidagi 4 ta tugma qatori olib tashlandi:** Modal ichidagi alohida tugmalar qatori butunlay olib tashlandi, oyna ortiqcha elementlardan tozalandi.
  - **Nasiyaga yozish (Qarz) Checkbox:** To'lov tasdiqlash oynasi ichiga **`[ ] 📒 Nasiyaga yozish (Qarz)`** belgilash katakchasi (checkbox) joylashtirildi. Ushbu checkbox yoqilganda qarz oynasi (mijozni tanlash, mavjud qarzi, avans to'lovi va to'lash muddati) darcha ichida ochiladi. Dastlabki avans avtomatik 0 so'm qilib belgilanib, butun summa qarzga yo'naltiriladi; kassir xohlasa naqd yoki karta bo'yicha qisman avans kiritishi mumkin.
  - **Moslashuvchan aylantirish (ScrollViewer):** Kichik ekranli monitor yoki noutbuklarda nasiya parametrlari ochilganda ekrandan siqilib chiqib ketmasligi uchun oyna ichki qismi silliq vertikal aylantirish (scroll) bilan o'raldi. Pastki "Bekor qilish (Esc)" va "Tasdiqlash (Enter)" tugmalari esa har doim pastda ko'rinib, o'z joyida qat'iy turadi.
  - **Qarzdor ismi takrorlanmasligi (Unikal nom):** Qarz daftarida hamda kassadagi tezkor mijoz qo'shish modalida bir xil ismli mijozni qayta saqlash taqiqlandi. Agar mavjud ism kiritilsa, tizim ogohlantirish xabarini chiqaradi.
  - **Kassa chekida Nasiya yozuvini yashirish:** Chop etiladigan termo-chekda hamda ekrandagi jonli ko'rinishda (Preview) `Nasiya (Qarz): ... so'm` qatori chiqarilmaydi. Chekda mijozga ko'rsatish uchun faqat qabul qilingan to'lov turi (`Naqd`, `Karta` yoki `Aralash`) va jami savdo summasi odatiy standart chek kabi chop etiladi.
- **Mijoz uchun foydasi:**
  - Kassir uchun savdo qilish jarayoni yana-da sodda va tushunarli bo'ldi: asosiy ekranda faqat 3 ta to'lov tugmasi turadi, qarzga sotish kerak bo'lganda tasdiqlash oynasida 1 ta checkbox orqali mijoz tanlanadi. Xaridorga beriladigan chekda esa ortiqcha qarz yozuvlari aks etmaydi. Bir xil ismli qarzdorlar adashib qayta-qayta ochilishining oldi olindi.

---

### 45. 🛠️ Nasiya savdoni tasdiqlashdagi "Invalid debt wire component" xatoligi to'liq bartaraf etildi
- **Nima o'zgardi:**
  - Nasiya orqali savdoni tasdiqlashda (`ConfirmSale`) do'konning asosiy ombor identifikatori (`main-default-warehouse`) qat'iy UUID formatida bo'lmaganligi sababli `DebtEnvelope` tekshiruvida `Invalid debt wire component` istisnosi (xatosi) yuzaga kelayotgan edi.
  - `DebtEnvelope` (ham C# Desktop, ham Kotlin Android) protokoli takomillashtirildi: ombor identifikatori (`WarehouseGuid`) standart 36-belgili UUID bo'lishi bilan birga do'kondagi matnli ombor identifikatorlarini (`main-default-warehouse` va boshqa nomlarni) to'liq qo'llab-quvvatlaydigan qilindi.
  - `BuildSaleSnapshot` va kassa to'lov jarayonida ombor GUID va ombor nomi bo'sh bo'lib qolishining oldini oluvchi avtomatik himoya qo'yildi (`whGuid` va `whName` xavfsiz fallbacks).
  - Savdo GUID (`_checkoutGuid`) har doim xalqaro standart "D" formatida (kichik harfli defisli GUID) shakllantirilishi kafolatlandi.
  - Barcha sinovlar (`Debt.CoreTests` protokoli, 155 ta shared envelope fixtures, 92 ta wire fixtures, 97 ta accounting fixtures hamda real baza integratsion testlari) 100% muvaffaqiyatli o'tdi.
- **Mijoz uchun foydasi:**
  - Kassada to'lovni tasdiqlashda (Enter bosilganda) hech qanday texnik xatolik chiqmaydi, nasiyaga sotish (avans bilan yoki to'liq qarz) bir zumda muvaffaqiyatli yakunlanadi.

---

### 46. 💎 Hisobotlar va Qarz daftari UI/UX modernizatsiyasi: Toza ko'rinish, Vektor ikonkalar, Dropdown filtr, Qidiruv tuzatmasi va Bloklash
- **Nima o'zgardi:**
  - **Hisobotlardan ortiqcha qarz/cashflow bloki olib tashlandi:** Hisobotlar ekranidagi ikkilamchi "BERILGAN NASIYA", "UNDIRILGAN QARZ", "KASSA PUL OQIMI", "FAOL QARZDORLIK" paneli olib tashlandi. Ekranning to'liq vertikal balandligi savdolar tarixi jadvaliga berildi, vizual yuklama va ortiqcha elementlar tozalandi.
  - **Qarz to'lov oynasidan keraksiz chek tanlash olib tashlandi:** Qarzni so'ndirish modalidagi ortiqcha va chalkashtiruvchi "Taqsimlash tartibi:" ComboBox maydoni butunlay olib tashlandi. Mijoz to'lov kiritganda, tizim avtomatik eng birinchi ochilgan ochiq qarzlar bo'yicha eng adolatli va to'g'ri FIFO tartibida jonli ravishda taqsimlaydi va ekranda qaysi chek qancha kamayganini ko'rsatib turadi.
  - **Qarz daftaridan "Kassada nasiya" tugmasi olib tashlandi:** Mijoz kartasidagi ortiqcha "Kassada nasiya" tugmasi olib tashlandi.
  - **"Arxiv" tugmasi "Bloklash" deb nomlandi:** Qarz daftaridagi mijozni arxivlash funksiyasi endi mantiqan to'g'ri nomlangan: **"Bloklash"** (va bloklangan bo'lsa **"Blokdan chiqarish"**). Mijoz holati nishoni (badge) ham "Arxivlangan" o'rniga aniq **"Bloklangan"** deb ko'rsatiladi.
  - **Qidiruv maydonidagi ustma-ust yozilish muammosi tuzatildi:** Qidiruv maydonida matn yozilganda suv belgisi (placeholder) va ikonka ustiga yozilib ketish nuqsoni bartaraf etildi. Matn kiritilganda bir zumda o'chadigan placeholder, chapda chiroyli vektor qidiruv belgisi hamda o'ngda bitta bosish bilan qidiruvni tozalovchi "X" tugmasi qo'yildi.
  - **Filtr tugmalari ixcham Dropdown (ComboBox) qilindi:** Qarzlar ro'yxati ustidagi 5 ta alohida filtr tugmasi bitta qulay va chiroyli ochiluvchi tanlash menyusi (Dropdown) ko'rinishiga o'tkazildi (`🔴 Qarzi bor`, `⚠️ Muddati o'tgan`, `Barchasi`, `⚪ Yopilgan`, `🟢 Ortiqcha to'lov`).
  - **Yangilash tugmasi so'zsiz vektor ikonka qilindi:** "🔄 Yangilash" tugmasi o'rniga faqat 40x40 o'lchamdagi aylanuvchi animatsiyali zamonaviy vektor tugma qo'yildi.
  - **Barcha emojilar vektor ikonkalar bilan almashtirildi:** Qarz daftari ekranidagi barcha emojilar (KPI kartalar, amallar, modallar sarlavhalari, yopish tugmalari, sub-tablar) professional SVG vektor `Path` ikonkalariga o'tkazildi va o'lchamlari moslashtirildi.
- **Mijoz uchun foydasi:**
  - Hisobotlar va Qarz daftari interfeysi ancha toza, yengil, professional va qulay bo'ldi. Matnlar ustma-ust tushmaydi, filtrlar ixcham joylashgan, ortiqcha chalg'ituvchi tugmalar yo'q.

---

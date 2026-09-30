# Line kassa (Desktop) — Rasmiy Foydalanish Qo'llanmasi

**Hujjat versiyasi:** 1.0.0 (Release)  
**Platforma:** Windows 10 / 11 (64-bit)  
**Tizim turi:** Oflayn Kassa va Ombor Nazorati  
**HTML Shakli:** [LinePOS_Desktop_Foydalanish_Qollanmasi.html](file:///c:/Users/Acer/Documents/POS/PROD/Instruksiya/LinePOS_Desktop_Foydalanish_Qollanmasi.html)

---

## Mundarija

- [1.0 Tizim Umumiy Ko'rinishi va Boshqaruv Asoslari](#10-tizim-umumiy-korinishi-va-boshqaruv-asoslari)
  - [1.1 Dasturning maqsadi va texnik afzalliklari](#11-dasturning-maqsadi-va-texnik-afzalliklari)
  - [1.2 Asosiy oyna anatomiyasi](#12-asosiy-oyna-anatomiyasi)
  - [1.3 Avtomatik klaviatura nazorati](#13-avtomatik-klaviatura-nazorati)
- [2.0 Kassa Bo'limi (Sotuv va Savat Boshqaruvi)](#20-kassa-bolimi-sotuv-va-savat-boshqaruvi)
  - [2.1 Shtrix-kod skaneri bilan tezkor sotish](#21-shtrix-kod-skaneri-bilan-tezkor-sotish)
  - [2.2 Nom bo'yicha qidiruv](#22-nom-boyicha-qidiruv)
  - [2.3 Eng ko'p sotilgan tovarlar (Top Sellers)](#23-eng-kop-sotilgan-tovarlar-top-sellers)
  - [2.4 Savat tarkibi va Narx turlari (1-narx / 2-narx)](#24-savat-tarkibi-va-narx-turlari-1-narx--2-narx)
  - [2.5 Narxni tahrirlash (Sotuvchi chegirmasi)](#25-narxni-tahrirlash-sotuvchi-chegirmasi)
  - [2.6 Kutishga qo'yish (Hold / Multi-Cart)](#26-kutishga-qoyish-hold--multi-cart)
  - [2.7 Kassada topilmagan shtrix-kodni darhol qo'shish](#27-kassada-topilmagan-shtrix-kodni-darhol-qoshish)
  - [2.8 To'lov turlari va savdoni yakunlash](#28-tolov-turlari-va-savdoni-yakunlash)
- [3.0 Ombor Bo'limi (Inventar va Tovar Nazorati)](#30-ombor-bolimi-inventar-va-tovar-nazorati)
  - [3.1 Ko'p omborli tuzilma va omborlararo ko'chirish](#31-kop-omborli-tuzilma-va-omborlararo-kochirish)
  - [3.2 Kategoriyalar ierarxiyasi](#32-kategoriyalar-ierarxiyasi)
  - [3.3 Yangi tovar kiritish parametrlari](#33-yangi-tovar-kiritish-parametrlari)
  - [3.4 Aqlli dublikat himoyasi](#34-aqlli-dublikat-himoyasi)
  - [3.5 Kam qolgan va manfiy qoldiqlar nazorati](#35-kam-qolgan-va-manfiy-qoldiqlar-nazorati)
  - [3.6 Excel formatida eksport](#36-excel-formatida-eksport)
- [4.0 Printerlar va Shtrix-kod Etiketkalarini Chop Etish](#40-printerlar-va-shtrix-kod-etiketkalarini-chop-etish)
  - [4.1 Shtrix-kod stiker chiqarish oynasi](#41-shtrix-kod-stiker-chiqarish-oynasi)
  - [4.2 Stiker o'lchamlari va millimetrik siljitish](#42-stiker-olchamlari-va-millimetrik-siljitish)
  - [4.3 Chek printerlari (58 mm va A4)](#43-chek-printerlari-58-mm-va-a4)
- [5.0 Hisobotlar va Moliyaviy Tahlil](#50-hisobotlar-va-moliyaviy-tahlil)
  - [5.1 Vaqt bo'yicha tahlil](#51-vaqt-boyicha-tahlil)
  - [5.2 Asosiy KPI ko'rsatkichlari](#52-asosiy-kpi-korsatkichlari)
  - [5.3 Cheklar arxivi va qayta chop etish](#53-cheklar-arxivi-va-qayta-chop-etish)
- [6.0 Wi-Fi Sinxronizatsiya (Mobil Bilan Bog'lanish)](#60-wi-fi-sinxronizatsiya-mobil-bilan-boglanish)
  - [6.1 Telefon bilan internetsiz ulanish](#61-telefon-bilan-internetsiz-ulanish)
  - [6.2 Jonli (Real-Time SSE) sinxronizatsiya](#62-jonli-real-time-sse-sinxronizatsiya)
  - [6.3 Yo'nalishli o'tkazish va Aqlli Birlashtirish](#63-yonalishli-otkazish-va-aqlli-birlashtirish)
  - [6.4 Windows Firewall (Port 8080) ruxsati](#64-windows-firewall-port-8080-ruxsati)
- [7.0 Ma'lumotlar Xavfsizligi va Zaxiralash (Backup)](#70-malumotlar-xavfsizligi-va-zaxiralash-backup)
- [8.0 Tezkor Tugmalar (Hotkeys) Jamlanmasi](#80-tezkor-tugmalar-hotkeys-jamlanmasi)
- [9.0 Muammolarni Hal Qilish (Troubleshooting)](#90-muammolarni-hal-qilish-troubleshooting)

---

## 1.0 Tizim Umumiy Ko'rinishi va Boshqaruv Asoslari

### 1.1 Dasturning maqsadi va texnik afzalliklari
**Line kassa** — do'konlar (elektr jihozlari, xo'jalik mollari, chakana va ulgurji savdo nuqtalari) uchun ishlab chiqilgan zamonaviy kompyuter kassa va ombor tizimidir.

- **100% Oflayn ishlash:** Dastur ishlashi uchun internet aloqasi talab etilmaydi. Barcha ma'lumotlar kompyuterning lokal xotirasida (SQLite) saqlanadi.
- **Yuqori tezlik va past resurs sarfi:** Dastur zamonaviy .NET 8 WPF platformasida yaratilgan. Kuchsiz protsessorli (masalan, Intel Celeron) kassa kompyuterlarida ham qotmasdan ishlaydi, xotira (RAM) sarfi o'rtacha 45–70 MB ni tashkil etadi.
- **Virtualizatsiya:** Omborda 10,000 dan ortiq tovar bo'lsa ham jadval bir lahzada silliq aylanadi.

### 1.2 Asosiy oyna anatomiyasi
Dastur ochilganda har doim to'g'ridan-to'g'ri **🛒 Kassa** ekranida ish boshlaydi. Yuqori navigatsiya panelida quyidagi asosiy tugmalar joylashgan:
- **🛒 Kassa:** Xaridorlarga xizmat ko'rsatish, tovarlarni savatga yig'ish va to'lov qabul qilish ekrani.
- **📦 Ombor:** Tovar va mahsulotlar qoldig'i, yangi tovar qo'shish, tahrirlash, stiker chiqarish va omborlararo ko'chirish.
- **📊 Hisobotlar:** Savdo aylanmasi, kunlik/oylik tushum, sof foyda, nasiyalar hisobi va cheklar arxivi.
- **📱 Wi-Fi Sinxron:** Do'kon ichidagi Android telefonlar bilan kompyuterni internetsiz Wi-Fi orqali bog'lash markazi.
- **Yuqori o'ng burchakdagi vositalar:**
  - `✏️` **Karta solig'i foizi:** Terminal orqali to'lovlarda qo'shiladigan komissiya/soliq foizini sozlash (masalan: 1.8%).
  - `💾 Baza zaxirasi`: Bazaning to'liq xavfsiz nusxasini fleshkaga yoki diskka yuklab olish.
  - `📞 Bog'lanish`: Dastur ishlab chiquvchisi bilan aloqa ma'lumotlari.

### 1.3 Avtomatik klaviatura nazorati (Auto English & Transliteration)
Sotuv paytida shtrix-kod skanerlash yoki qidiruvda klaviatura tili noto'g'ri (ruscha/o'zbekcha) bo'lib qolishi natijasida tovar topilmasligi holatlarining oldi to'liq olingan:
> Dastur oynasiga o'tishingiz bilan Windows klaviatura tili avtomatik ravishda **English (US)** rejimiga o'tkaziladi. Agar qidiruv maydoniga ruscha harflar (masalan, `Ф`) kiritilsa, tizim uni avtomatik tarzda inglizcha ekvivalentiga (`A`) almashtirib to'g'ri tovar nomini topadi.

---

## 2.0 Kassa Bo'limi (Sotuv va Savat Boshqaruvi)

### 2.1 Shtrix-kod skaneri bilan tezkor sotish
Dasturda apparatli USB / Bluetooth shtrix-kod skanerlari bilan ishlash maksimal qulaylashtirilgan:
- **Global ushlash (Fokus talab etilmaydi):** Kassir ekranning qaysi qismida turgan bo'lishidan qat'i nazar, shtrix-kod skanerlanganda tovar avtomatik ravishda savatchaga **1 dona** qo'shiladi. Qidiruv qatorini sichqoncha bilan bosish shart emas.
- Shtrix-kod skanerlangach, qidiruv maydoni tozalangan holda keyingi mahsulotga tayyor turadi.

### 2.2 Nom bo'yicha qidiruv
1. Yuqori qidiruv maydoniga tovar nomining dastlabki harflarini yozing (masalan, `avtomat 16a`).
2. Pastda mos tovarlar ro'yxati chiqadi. Birinchi tovarni darhol savatga qo'shish uchun klaviaturadan `Enter` tugmasini bosing.
3. Yoki kerakli tovar kartasiga sichqoncha bilan 1 marta bosing.

### 2.3 Eng ko'p sotilgan tovarlar (Top Sellers)
Qidiruv maydoni bo'sh bo'lgan paytda ekranning chap qismida **⭐ Eng ko'p sotiladigan tovarlar** bloki chiqib turadi. Ko'p sotiladigan mayda yoki shtrix-kodi yo'q mahsulotlarni qidirmasdan, shu yerdan bitta bosish orqali savatga qo'shishingiz mumkin.

### 2.4 Savat tarkibi va Narx turlari (1-narx / 2-narx)
- **Miqdorni o'zgartirish:** `+` va `-` tugmalari orqali yoki soni ustiga bosib kerakli miqdorni to'g'ridan-to'g'ri yozish mumkin (o'nlik kasrlar ham qo'llab-quvvatlanadi, masalan: `2.5` metr).
- **Butun savat narxini almashtirish (1-narx / 2-narx):** Savatning yuqori o'ng qismidagi **↔️ Narx almashtirish** tugmasini bossangiz, savatdagi barcha tovarlar ulgurji (2-narx) yoki chakana (1-narx) qiymatiga bir zumda o'tadi.
- **Tovarni savatdan o'chirish:** Tovar yonidagi qizil `✕` tugmasini bosing.
- **Savatni to'liq tozalash:** Yuqoridagi `🗑️` (quti) tugmasini bosing.

### 2.5 Narxni tahrirlash (Sotuvchi chegirmasi)
> **Qo'llash tartibi:** Savatdagi tovar narxi ustiga sichqoncha bilan **ikki marta tez bosing (Double-click)**. Kichik tahrirlash oynasi ochiladi. Yangi narxni kiriting va `Enter` bosing. Mazkur narx faqat joriy chek uchun amal qiladi, ombordagi asosiy narx o'zgarmasdan saqlanib qoladi.

### 2.6 Kutishga qo'yish (Hold / Multi-Cart)
1. Savat tepasidagi **⏸️ Kutishga qo'yish (Hold)** tugmasini bosing.
2. Joriy savat vaqtincha xotiraga olinadi va yangi xaridor uchun savatcha bo'shatiladi.
3. Kutishga qo'yilgan savat yuqorida sariq yorliq (tab) ko'rinishida paydo bo'ladi (masalan: `Mijoz 1 (3 ta - 150 000 so'm)`). Xaridor qaytgach, ushbu yorliqqa bitta bosish orqali savatni darhol tiklab, savdoni davom ettirish mumkin.

### 2.7 Kassada topilmagan shtrix-kodni darhol qo'shish
Agar yangi kelgan tovarning shtrix-kodi skanerlanganda u hali omborga kiritilmagan bo'lsa:
- Ekranda *"Topilmagan shtrix-kod. Yangi tovar sifatida qo'shilsinmi?"* oynasi chiqadi.
- `Enter` tugmasini bossangiz, dastur to'g'ridan-to'g'ri Ombor bo'limidagi **Tovar qo'shish** oynasini ochadi va shtrix-kod maydoni avtomatik to'ldirilgan bo'ladi. Tovar nomi va narxini yozib saqlasangiz, u yana avtomatik savatga qo'shiladi.

### 2.8 To'lov turlari va savdoni yakunlash
Savat yig'ilgach, pastdagi yirik yashil **🛒 SOTISH (jami summa)** tugmasini yoki klaviaturadan `Enter` bosing. To'lov oynasi ochiladi:

| To'lov Turi | Tavsif va Ishlash Tartibi | Xususiyatlari |
| :--- | :--- | :--- |
| **💵 Naqd pul** | Xaridor bergan summa kiritiladi. Tizim qaytimni avtomatik hisoblab ko'rsatadi. | Standart to'lov turi. |
| **💳 Karta (Terminal)** | Humo yoki Uzcard orqali to'lov. | Agar tizimda karta solig'i sozlangan bo'lsa (masalan 1.8%), u avtomatik ravishda qo'shilib hisoblanadi. |
| **⚖️ Aralash to'lov** | Summaning bir qismi naqd, qolgani karta orqali to'lanadi. | Har ikkala summa kiritilganda qoldiq avtomatik to'g'irlanadi. |
| **📝 Nasiya (Qarz)** | Doimiy xaridorlar uchun to'lovni keyinroqqa qoldirish. | Xaridorning ismi va telefon raqami kiritilishi shart. Nasiyalar Hisobotlar bo'limida alohida nazorat qilinadi. |

**Printer tanlash opsiyasi:**
- `🚫 Chek chiqarilmasin` — Qog'oz sarflamasdan faqat bazada savdoni saqlash (standart holat).
- `🧾 Xprinter 58mm` — 58 mm termal lenta chekini bir zumda bosib chiqarish.
- `📄 A4 Printer` — Rasmiy schyot-faktura yoki tovar cheki shaklida A4 qog'ozga chiqarish.

To'lovni tasdiqlash uchun `Enter`, bekor qilish uchun `Esc` tugmasidan foydalaniladi.

---

## 3.0 Ombor Bo'limi (Inventar va Tovar Nazorati)

### 3.1 Ko'p omborli tuzilma va omborlararo ko'chirish
- **Omborlar ro'yxati:** Do'kondagi "Asosiy do'kon", "Ichki ombor", "Filial" kabi omborlar yaratilishi mumkin.
- **Tovar ko'chirish (Transfer):** Bir ombordagi tovarlarni boshqa omborga ko'chirish uchun `↔️ Ko'chirish` tugmasidan foydalaniladi. Ko'chirilayotgan tovar miqdori jo'natuvchi omborda kamayadi, qabul qiluvchi omborda esa ko'payadi.

### 3.2 Kategoriyalar ierarxiyasi
Omborda tovarlar qulay kartalar shaklida kategoriyalarga ajratilgan. Yangi kategoriya ochish uchun `➕ Yangi kategoriya` tugmasini bosib, nomini kiritish kifoya.

### 3.3 Yangi tovar kiritish parametrlari

| Maydon Nomi | Majburiyligi | Tavsif |
| :--- | :--- | :--- |
| **Tovar nomi** | Majburiy | Aniq va tushunarli nom (masalan: *Rozetka Viko oq*). |
| **Shtrix-kod** | Ixtiyoriy | Skanerlash orqali kiritiladi yoki `⚡ Generatsiya` tugmasi orqali noyob kod hosil qilinadi. |
| **Kategoriya** | Majburiy | Mavjud toifalardan biri tanlanadi. |
| **O'lchov birligi** | Majburiy | `dona (sht)` yoki `metr (m)` tanlanadi. |
| **Tannarx (Kirim)** | Majburiy | Tovarning kelish bahosi. **SO'M** yoki **USD ($)** da kiritilishi mumkin. USD tanlansa, MB kursi bo'yicha so'mga avtomatik o'giriladi. |
| **1-Sotish narxi** | Majburiy | Chakana savdo uchun asosiy narx. |
| **2-Sotish narxi** | Ixtiyoriy | Ulgurji yoki usta/doimiy mijozlar uchun ikkinchi arzonlashtirilgan narx. |
| **Qoldiq soni** | Majburiy | Omborda mavjud haqiqiy miqdor. |
| **Minimal ogohlantirish** | Ixtiyoriy | Ushbu sondan kam qolganda dastur ogohlantiradi (standart: 3 ta). |
| **Izoh / Eslatma** | Ixtiyoriy | Yetkazib beruvchi, mahsulot joylashuvi yoki boshqa eslatmalar. |

### 3.4 Aqlli dublikat himoyasi
> **Qanday ishlaydi:** Katta-kichik harflar, so'zlar orasidagi ortiqcha bo'shliqlar va o'zbekcha tutuq belgilari (`'`, `’`, `‘`, `ʻ`) normallashtirilib solishtiriladi. Agar bunday tovar avval kiritilgan bo'lsa, tizim qizil ogohlantirish bilan saqlashni bloklaydi va mavjud tovarni ko'rsatadi.

### 3.5 Kam qolgan va manfiy qoldiqlar nazorati
- **⚠️ Kam qolgan tovarlar toifasi:** Qoldig'i belgilangan me'yordan kam qolgan barcha tovarlarni bitta bosish bilan filtrlash imkonini beradi.
- **Manfiy qoldiqlar (Qizil belgi):** Agar tovar omborda 0 ta bo'lsa-yu, ammo do'konda sotilsa, tizim savdoni to'xtatmaydi, balki qoldiqni `⚠️ -1 ta (Ortiqcha sotuv)` ko'rinishida qizil qilib belgilaydi. Bu orqali omborchi kelgan partiyani kirim qilganda qoldiq to'g'irlanadi.

### 3.6 Excel formatida eksport
Ombordagi barcha tovarlar ro'yxatini, ularning qoldig'i, tan narxi va sotish narxlarini to'liq formatlangan jadval ko'rinishida **Excel (.xlsx)** fayliga yuklab olish uchun ombor oynasidagi **📊 Excelga eksport** tugmasini bosing.

---

## 4.0 Printerlar va Shtrix-kod Etiketkalarini Chop Etish

### 4.1 Shtrix-kod stiker chiqarish oynasi
1. Ombor bo'limida kerakli tovar qatoridagi **🏷️ Stiker** tugmasini bosing.
2. Ochilgan oynada chap tomonda **Jonli ko'rinish (Preview)** stiker qog'ozida qanday chiqishini ko'rsatib turadi.
3. Kerakli nusxalar sonini kiriting va **🖨️ Chop etish** tugmasini bosing.

### 4.2 Stiker o'lchamlari va millimetrik siljitish
- **Qo'llab-quvvatlanadigan o'lchamlar:** `40 × 30 mm`, `30 × 20 mm`, `50 × 30 mm`, `58 × 40 mm` (Xprinter XP-365B va analoglari uchun).
- **↔️ Siljitish (Offset) sozlamasi:** Qog'oz chetga surilib qolsa, `◀` va `▶` tugmalari orqali 0.5 mm qadamda matnni chapga yoki o'ngga surish mumkin.
- **Ko'rsatish filtri:** Do'kon nomi, tovar nomi, shtrix-kod va narxni ko'rsatish/yashirish katakchalari.

### 4.3 Chek printerlari (58 mm va A4)
- **58 mm Kassa cheki:** Termal lentalarda xarid tafsilotlarini ixcham va chiroyli chop etadi.
- **A4 Rasmiy faktura:** Tashkilot va ulgurji xaridorlar uchun to'liq jadval va hisob-faktura ko'rinishida bosib beradi.

---

## 5.0 Hisobotlar va Moliyaviy Tahlil

### 5.1 Vaqt bo'yicha tahlil
- `Bugun` — Bugungi kun tushumi.
- `Kecha` — Kechagi kun yakuni.
- `Bu oy` — Joriy oyning 1-kunidan to'plangan statistika.
- `Oraliq sana (Kalendar)` — Erkin tanlangan oraliq sana.

### 5.2 Asosiy KPI ko'rsatkichlari
1. **Jami Savdo:** Umumiy aylanma summasi.
2. **Sof Foyda:** Savdo summasidan tovarlarning tannarxi ayirib tashlangan daromad.
3. **Naqd Pul:** Kassadagi sof naqd mablag'.
4. **Plastik Karta:** Terminal orqali tushgan mablag'.
5. **Nasiyalar:** Hali undirilmagan qarzlar.

### 5.3 Cheklar arxivi va qayta chop etish
Barcha amalga oshirilgan savdolar ro'yxatida har bir chekning ichki tarkibini ko'rish va zarurat tug'ilganda `🖨️ Chekni qayta chiqarish` tugmasi orqali qog'ozga qayta chop etish mumkin.

---

## 6.0 Wi-Fi Sinxronizatsiya (Mobil Bilan Bog'lanish)

### 6.1 Telefon bilan internetsiz ulanish
1. Kompyuter va telefon bir xil Wi-Fi tarmog'iga ulangan bo'lishi lozim (internet shart emas).
2. Dasturda **📱 Wi-Fi Sinxron** oynasini oching. Ekranda kompyuterning IP manzili va ulanish QR-kodi ko'rinadi.
3. Telefondagi **Line Kassa** mobil ilovasida QR-kodni skanerlang. Ulanish darhol o'rnatiladi.

### 6.2 Jonli (Real-Time SSE) sinxronizatsiya
Ulanish o'rnatilgach:
- Kompyuterda tovar yoki narx o'zgarsa — bir necha millisekundda telefonlarda aks etadi.
- Telefondan sotuv qilinsa — kompyuterdagi qoldiq darhol kamayadi va hisobotlarga qo'shiladi.

### 6.3 Yo'nalishli o'tkazish va Aqlli Birlashtirish
- **📤 Telefondan ➔ Kompyuterga:** Telefondagi savdo va yangi tovarlarni kompyuterga ko'chirish.
- **📥 Kompyuterdan ➔ Telefonga:** Kompyuterdagi bazani telefonga yuklash.
- **🔄 Aqlli birlashtirish:** Hech qaysi ma'lumot yo'qolmasligi uchun ikkala qurilmadagi yangiliklarni xavfsiz birlashtirish.

### 6.4 Windows Firewall (Port 8080) ruxsati
Agar telefon kompyuterga ulanmasa, Wi-Fi Sinxron sahifasidagi **"🛡️ Firewall portini ochish"** tugmasini bosing. Dastur Windows xavfsizlik devoridan 8080-portni avtomatik tarzda ochib beradi.

---

## 7.0 Ma'lumotlar Xavfsizligi va Zaxiralash (Backup)

### 7.1 Qo'lda zaxira nusxasini olish
1. Yuqori o'ng burchakdagi **💾 Baza zaxirasi** tugmasini bosing.
2. Faylni saqlash joyi (USB fleshka yoki disk)ni tanlang.
3. Baza butun holatda `.db` fayli shaklida saqlanadi. Fleshkani haftada bir marta yangilab borish tavsiya etiladi.

### 7.2 Foniy avtomatik zaxiralash (Silent Auto-Backup)
Telefon kompyuterga har gal sinxronizatsiya qilganida, dastur avtomatik ravishda oxirgi 15 ta zaxira nusxasini kompyuterning ichki xotirasida (`AppData/Backups`) saqlab boradi.

---

## 8.0 Tezkor Tugmalar (Hotkeys) Jamlanmasi

| Tugma | Qaysi Oynada | Vazifasi |
| :--- | :--- | :--- |
| `Enter` | Qidiruv maydonida | Topilgan birinchi tovarni savatga 1 dona qo'shish |
| `Enter` | To'lov oynasida | To'lovni tasdiqlash va savdoni yakunlash |
| `Esc` | Har qanday modal oynada | Oynani bekor qilish va yopish |
| `Double Click` | Savatdagi narx ustida | Tovarga joriy chek uchun chegirma berish |
| `Skanerlash` | Kassaning istalgan joyida | Shtrix-kod bo'yicha tovarni darhol savatga tashlash |
| `Enter` | Yangi shtrix-kod modalida | Topilmagan shtrix-kod bilan tovar qo'shishga o'tish |
| `Esc` | Yangi shtrix-kod modalida | Xatolikni bekor qilish |

---

## 9.0 Muammolarni Hal Qilish (Troubleshooting)

1. **Skaner shtrix-kodni o'qiyapti, ammo savatga tushmayapti?**
   - Skaneringiz *USB HID (Keyboard emulation)* rejimida ekanligiga va kod oxirida *Enter* signali yuborayotganiga ishonch hosil qiling.
2. **Telefon kompyuterga ulanmayapti?**
   - Kompyuter va telefon bir xil Wi-Fi tarmog'idaligini tekshiring.
   - Wi-Fi Sinxron sahifasidagi **"🛡️ Firewall portini ochish"** tugmasini bosing.
3. **Termoprinter chek chiqarmayapti?**
   - To'lov oynasidagi printerlar ro'yxatida printeringiz (`XP-58` yoki `XP-365B`) tanlanganligini va USB kabeli mustahkam ulanganligini tekshiring.

---

*© 2026 Line POS — Rasmiy texnik hujjat.*

# 📋 LinePOS — Oxirgi Yangilanishlar

Ushbu qisqacha qo'llanma mijozga oxirgi kiritilgan qulayliklarni ko'rsatish uchun tayyorlandi:

---

### 1. 📦 Tovarlar jadvalining ixchamlashishi va ustunlar yangi tartibi
- **Nima o'zgardi:** 
  - Kam ishlatiladigan ustunlar (**Kategoriya**, **Birlik**, **2-sotish narxi**) asosiy jadvaldan olib tashlandi.
  - Ustunlar tartibi eng ko'p ishlatiladigan qulay ketma-ketlikka keltirildi:
    1. **Mahsulot nomi**
    2. **Amallar (✏️ Tahrirlash, 🖨️ Shtrix-kod chiqarish, 🗑️ O'chirish)** — darhol nomining yonida turadi!
    3. **Shtrix-kod**
    4. **Izoh (Zakaz uchun)**
    5. **Tan narxi**
    6. **Sotish narxi**
    7. **Qoldiq**
- **Mijoz uchun foydasi:** 
  - Jadval noutbuk va barcha kompyuter ekranlariga 100% to'liq sig'adi — yonga scroll qilishga (surishga) mutlaqo hojat qolmadi!
  - Har doim eng ko'p bosiladigan tugmalar (tahrirlash, stiker chiqarish, o'chirish) ko'z oldida, tovar nomining yonida turadi.
  - Tovar haqidagi qolgan barcha ma'lumotlarni ko'rish yoki o'zgartirish uchun tovar ustiga **2 marta bosish** kifoya.

---

### 2. 🔤 Mahsulot nomi birinchi harfi avtomatik KATTA harf bo'ladi
- **Nima o'zgardi:** Yangi tovar qo'shganda yoki tahrirlaganda, tovar nomini yozish bilan birinchi harf avtomatik tarzda **KATTA** harf qilib olinadi (masalan: `kabel` deb yozilsa, darhol `Kabel` bo'lib yoziladi).
- **Mijoz uchun foydasi:** Har safar bosh harf yozish uchun `Shift` tugmasini bosib o'tirish shart emas, yozish juda tezlashadi va qulay bo'ladi.
- **Muhim jihati:** Agar tovar nomi raqam bilan boshlansa (masalan: `2_ombor` yoki `10mm sim`), o'z holicha saqlanadi, raqam buzilmaydi. Bazaga ham bosh harf bilan chiroyli tartibda saqlanadi.

---

### 3. 📒 "Qarzlar" (Qarz daftari) yangi bo'limi va kassada Nasiya savdosi
- **Nima o'zgardi:**
  - Asosiy menyuda yangi **📒 Qarzlar** bo'limi paydo bo'ldi.
  - Kassada to'lov turlariga **📒 Nasiya** varianti qo'shildi: mijozni tanlash (yoki joyida yangi qo'shish), avans to'lovi (naqd yoki karta), qoladigan qarz miqdori va to'lash muddatini belgilash imkoniyati yaratildi.
  - "Qarzlar" bo'limida barcha qarzdorlar ro'yxati, jami faol qarz, muddati o'tgan qarzlar, qidiruv va qulay filtrlar ("Qarzi bor", "Muddati o'tgan", "Yopilgan", "Ortiqcha to'lov", "Barchasi") o'rnatildi.
  - Qarz to'lovini qabul qilishda summani kiritish bilan qaysi nasiyalardan qancha yopilishi jonli ravishda oldindan ko'rsatiladi (preview), ortiqcha to'lov xatolik bilan kiritilishidan himoyalangan.
- **Mijoz uchun foydasi:** 
  - Nasiyaga savdo qilish va qarz yig'ish 100% oflayn rejimda, internet va Wi-Fi bo'lmaganda ham mustaqil ishlaydi.
  - Har bir mijoz bo'yicha to'liq cheklar va to'lovlar tarixi bir joyda shaffof ko'rinib turadi.

---

### 4. 📱 Android mobil ilovasida "Qarzlar" bo'limi va oflayn Nasiya kassa integratsiyasi
- **Nima o'zgardi:**
  - Mobil ilova pastki boshqaruv paneliga yangi **📒 Qarzlar** bo'limi qo'shildi (Kassa / Ombor / Qarzlar / Hisobotlar / Sozlamalar).
  - Mobil kassa to'lov oynasi 2x2 qulay ko'rinishga keltirildi (Naqd, Karta, Aralash, Nasiya).
  - Nasiya to'lovida mijozni qidirib tanlash, "+ Yangi" tugmasi orqali joyida tezkor mijoz ochish, naqd/karta avans summasini kiritish, qolgan qarz summasini avtomatik hisoblash va to'lov muddatini belgilash imkoniyati yaratildi.
  - Muzlatilgan savatlarda (Hold Carts) nasiya mijozi va to'lov holati saqlanadi hamda "📒 {Mijoz ismi} (Nasiya)" belgisi ko'rsatiladi.
  - "Qarzlar" ekranida 4 ta KPI kartasi (Jami nasiya, Muddati o'tgan, Jami to'langan, Haqdorlik), 5 ta filtr, mijozlar qidiruvi, mijoz kartasida cheklar va to'lovlar tarixi, jonli taqsimot ko'rsatuvchi "To'lov olish" dialogi hamda tanlangan mijoz bilan bir zumda kassaga o'tish ("Kassada ochish") imkoniyati yaratildi.
- **Mijoz uchun foydasi:**
  - Mobil telefonda internet va Wi-Fi bo'lmaganda ham 100% oflayn rejimda nasiyaga tovar sotish, yangi mijoz ochish va qarz to'lovlarini yig'ish mumkin.

---

### 5. 🔄 Nasiya tovarlarni qaytarish (Return), to'lovni bekor qilish va ortiqcha pul amallari
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

### 6. 📊 Qarzlar va Pul oqimi (Cashflow) hisoboti, Excel eksporti va mijoz ko'chirmasi (Sverka)
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

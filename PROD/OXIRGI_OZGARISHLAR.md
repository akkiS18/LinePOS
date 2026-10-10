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



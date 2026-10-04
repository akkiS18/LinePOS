# LinePOS: mahsulotni qaytarish funksiyasi rejasi

Holat: `fix/business-integrity` branchda amalga oshirilgan; PR #2 orqali tekshiriladi. Qaytarish `RT-…`, uni bekor qilish `RV-…` yozuvi bilan saqlanadi. Ishlatish va qurilmada sinash: [BUSINESS_FIXES_TEST_GUIDE_UZ.md](BUSINESS_FIXES_TEST_GUIDE_UZ.md).

## 1. Foydalanuvchi yo‘li

1. Hisobotlardan umumiy `LP-…` chek raqamini qidirish. Qidiruv butun tarixdan ishlaydi; natijada sana, jami, to‘lov turi va avvalgi qaytarishlar ko‘rinadi.
2. Chek tafsilotida **Qaytarish** tugmasi. Har qatorda sotilgan, oldin qaytarilgan va qaytarish mumkin bo‘lgan miqdor.
3. To‘liq chek yoki alohida mahsulotning bir qismini tanlash. Metr/kg kasrli miqdorni qo‘llaydi; dona uchun mavjud birlik siyosati saqlanadi.
4. Sabab, qabul qiluvchi ombor va holat: **qayta sotishga yaroqli** / **nuqsonli, alohida saqlash**.
5. Dastur eski sotuv narxi bo‘yicha qaytariladigan pulni ko‘rsatadi. Bugungi narx, ikkinchi narx yoki bugungi kurs qaytarish summasini o‘zgartirmaydi.
6. Pul qaytarish usuli va kassir tasdig‘i. Dastur qaydni saqlaydi; u bank terminalidan avtomatik pul o‘tkazilganini da’vo qilmaydi.
7. Qaytarish cheki: alohida `RT-…` raqami, original `LP-…`, mahsulotlar, pul, vaqt va kassir. Asl savdo o‘chirilmaydi.

## 2. Ma’lumotlar modeli

Android Room va desktop SQLite bir xil maydonlarga ega bo‘ladi:

- `returns`: `guid`, `sale_guid`, `created_at`, `operator_guid`, `reason`, `status`, `cash_refund`, `card_refund`, `fee_reversal`, `request_guid`, `authority_guid`.
- `return_items`: `guid`, `return_guid`, `sale_item_guid`, `product_guid`, `quantity`, `refund_amount_uzs`, `cost_reversal_uzs`, `warehouse_guid`, `disposition`, `original_usd_rate`.
- `sale_items`ga o‘zgarmas qator GUID’i: bir mahsulot bir chekda turli narx yoki ombordan kelgan bo‘lsa, qaytarish aynan kerakli qatorga bog‘lanadi.
- Avvaldan mavjud Android `refunds` jadvali tashlab yuborilmaydi. Eski jadval va summalar saqlanadi. Qator identifikatori va pul taqsimotini ishonchli tiklab bo‘lmaydigan eski qaytarish bor cheklar yangi qaytarishdan bloklanadi va tarixni qo‘lda tekshirish xabari chiqadi; ularni taxmin bilan yangi moliyaviy yozuvga aylantirish bajarilmaydi.
- Original chek va qaytarishlar tahrirlanmaydigan tarix bo‘ladi. Xato qaytarish keyingi qarama-qarshi operatsiya bilan bekor qilinadi; DELETE ishlatilmaydi.

## 3. Miqdor va ombor qoidalari

`qaytarish mumkin = sotilgan miqdor − tasdiqlangan qaytarilgan miqdor`.

Nol, manfiy, cheksiz yoki qolgan miqdordan katta qaytarish rad etiladi. Bu tekshiruv UI va tranzaksiya ichida takrorlanadi. Manfiy ombor qoldig‘iga ruxsat saqlanadi; bu ortiqcha qaytarishga ruxsat degani emas.

Yaroqli tovar tanlangan qabul omboriga qo‘shiladi. Nuqsonli tovar sotiladigan qoldiqqa qo‘shilmaydi: alohida nuqsonli qoldiq yoki karantin omboriga kiradi. Asl tannarxni qaytarish orqali bekor qilish va yana brak xarajati sifatida yozish birga bajarilmaydi — zarar ikki marta hisoblanmasligi shart.

## 4. Pul va foyda

- Qaytarish summasi original chekdagi real sotuv narxi va qaytarilgan miqdordan olinadi.
- Chegirma, ikkinchi narx va aralash to‘lovning tarixiy taqsimoti saqlanadi.
- Pul 0,01 so‘m aniqlikda yaxlitlanadi. Qisman qaytarishlar ketma-ketligi oxirida jami qaytarilgan pul original qatordagi puldan oshmaydi; oxirgi qaytarishda qolgan yaxlitlash farqi olinadi.
- Yaroqli tovar qaytsa, qaytarilgan qismning tarixiy tannarxi xarajatdan qaytariladi. Nuqsonli tovar qaytsa, tannarx zarari saqlanadi.
- Karta komissiyasi/solig‘i avtomatik ravishda qaytarilgan deb olinmaydi. Qaytarilgan haqiqiy summa bo‘lsa, u alohida qayd qilinadi; odatiy qiymat 0.
- USD hisobida original savdoning saqlangan kursi qo‘llanadi. Kursi yo‘q eski yozuvlarda tarixiy USD natijasi noma’lum bo‘lib qoladi.

Misol: narx 150 000, tarixiy tannarx 120 000, karta xarajati 2 700. Boshlang‘ich foyda 27 300. Tovar to‘liq va yaroqli qaytib, karta xarajati qaytarilmasa, savdo+qaytarish jami natijasi −2 700. Tovar nuqsonli bo‘lsa va tannarx tiklanmasa, jami zarar −122 700. Bu ikki holat alohida testlanadi.

## 5. Internet talab qilmaydigan sinxron va takroriy qaytarishdan himoya

Markaziy internet serveri qo‘shilmaydi. Juftlangan do‘konda desktop mahalliy Wi-Fi orqali qaytarishning yakuniy tasdig‘ini beradi:

1. Telefon noyob `request_guid` bilan so‘rov yuboradi.
2. Desktop bitta SQLite tranzaksiyasida qolgan qaytarish miqdorini tekshiradi, qaytarish, ombor o‘zgarishi va sinxron jurnalini yozadi.
3. Tasdiq javobi yo‘qolsa, telefon aynan shu GUID bilan qayta yuboradi. Desktop yangi qaytarish yaratmasdan avvalgi natijani qaytaradi.
4. Bir chekni ikkala qurilmadan bir vaqtda qaytarish sinovi majburiy: yig‘indi sotilgan miqdordan oshmaydi.
5. Desktop bilan aloqa bo‘lmasa, telefonda qaytarish loyihasini tayyorlash mumkin; pul qaytarildi deb yakuniy belgilash aloqa tiklanguncha kutiladi. Internetning yo‘qligi bunga to‘sqinlik qilmaydi — faqat mahalliy desktopga ulanish kerak.

Alohida, desktopga juftlanmagan telefon rejimi kerak bo‘lsa, u do‘konning yagona qaytarish tasdiqlovchisi sifatida ishlaydi. Tasdiqlovchini almashtirish alohida nazoratli jarayon bo‘ladi. Ikki uzilgan qurilmaga bir chekning qolgan miqdorini mustaqil yakuniy sarflashga ruxsat berilmaydi.

## 6. Hisobotlar

Filtrlar: **Barchasi / Savdo / Qaytarish / Brak**. Chekda **Qaytarilmagan / Qisman qaytarilgan / To‘liq qaytarilgan** holati.

Asl savdo asl sanasida qoladi. Qaytarish pul oqimi va foydaga qaytarish amalga oshirilgan sanada ta’sir qiladi; eski davr yashirincha qayta yozilmaydi. Asl chek tafsilotida barcha keyingi qaytarishlar ko‘rinadi.

Ekran va Excel bir hisoblash xizmatini ishlatadi. Alohida ko‘rsatkichlar: sotuv tushumi, qaytarilgan pul, sof tushum, tannarx qaytarilishi, karta xarajati, brak zarari va yakuniy foyda. Kategoriya/ombor filtri faqat tegishli qatorlarni hisoblaydi.

## 7. Eski cheklar va migratsiya

Yangi `LP-…` raqami bir xil GUID’dan olinadi va ikkala qurilmada bir xil. Avval qog‘ozga chiqarilgan qisqa lokal raqamdan uning boshqa qurilmadagi raqamini ishonchli tiklash har doim ham mumkin emas. Eski chek uchun sana, summa va tarkib bilan solishtirish ham saqlanadi; noto‘g‘ri chek avtomatik tanlanmaydi.

Migratsiyadan oldin backup olinadi. Yozuvlar soni, summalar, GUID’lar va ombor yig‘indilari oldin/keyin solishtiriladi. Eski sale-item GUID migratsiyasi ikkala qurilmada deterministik bo‘lishi alohida tekshiriladi; shubhali tarixiy bog‘lanishda qo‘lda tekshiruv talab qilinadi.

## 8. Amalga oshirish ketma-ketligi

1. Room/SQLite migratsiyalari va umumiy qaytarish modeli.
2. Original chek bo‘yicha hisoblash, pul taqsimoti va miqdor cheklovlari.
3. Desktop tranzaksiyasi, noyob so‘rovlar va qayta yuborish.
4. Wi-Fi protokoli, telefon loyihasi/tasdiq holatlari.
5. Desktop va mobil qaytarish oynalari, qaytarish cheki.
6. Hisobot, Excel, backup/restore bilan integratsiya.
7. Alohida test branchda avtomatik test va ikki haqiqiy qurilmada sinov; shundan keyin main’ga ko‘chirish.

## 9. Qabul testlari

- To‘liq va ketma-ket qisman qaytarish, oxirgi yaxlitlash farqi.
- Bir mahsulotning turli narxli ikki qatori, metr/kg, turli omborlar.
- Naqd/karta/aralash to‘lov, karta xarajati qaytishi va qaytmasligi.
- Sotuvdan keyin kurs, mahsulot nomi, kategoriya va narx o‘zgarishi.
- Bir tugmani tez ikki marta bosish, yo‘qolgan javob, restartdan keyin qayta yuborish.
- Ikki qurilmadan parallel qaytarish va aloqa uzilishi.
- Yaroqli/nuqsonli qaytish; tannarx zarari ikki marta hisoblanmasligi.
- Eski bazani migratsiya qilish, backupdan qayta tiklash va sinxronni davom ettirish.
- Hisobot va Excelda bir xil pul, filtr va chek soni.

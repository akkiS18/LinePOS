# 4-bosqich — desktop IP almashganda qayta ulanish

Branch: `fix/debt-stage3-complete`.

## Nima o‘zgardi

- Telefon eski IPga ulana olmasa, lokal Wi-Fi/Ethernet tarmog‘ida avval ulangan desktopni qidiradi. Qidirish UDP 8080, asosiy sinxron TCP 8080 orqali ishlaydi; tashqi server kerak emas.
- Javob server ID, yangi tasodifiy nonce va saqlangan token hashidan olingan HMAC orqali tekshiriladi. Bearer token va savdo/qarz ma’lumotlari broadcastga yuborilmaydi. Tekshiruvdan keyin odatiy autentifikatsiyalangan ping ham bajariladi.
- IP o‘zgarishida faqat manzil yangilanadi. Token, kursor, konflikt va offline navbatlar tozalanmaydi. Baza tiklash epochi esa mavjud sinxron oqimida alohida qayta ishlanadi.
- Shu desktopning QR kodi qayta skanerlansa, avval saqlangan ulanish tekshiriladi. U ishlasa, qayta pairing va kursorlarni nolga tushirish bo‘lmaydi; QR kodning pairing muddati tugagan bo‘lsa ham ishlaydi.
- Internetsiz Wi-Fi va mobil internet birga yoqilganda, lokal manzilga mos Wi-Fi/Ethernet tarmog‘i HTTP uchun tanlanadi.
- Fon urinishlari statusni qayta-qayta `Ulanmoqda`ga almashtirmaydi. Haqiqiy sync tugagach `Jonli`, hal qilinmagan konflikt bo‘lsa konflikt holati ko‘rinadi.
- UI maketi, Firebase, CBU va hisoblash qoidalari o‘zgartirilmadi.

## Test oldidan

Ikkala qurilmaga ham shu branchdan yangi versiyani o‘rnating. Amaldagi bazalarni zaxiralang; ilovani o‘chirish yoki bazani tozalash kerak emas.

Windows tarmog‘i ishonchli do‘kon tarmog‘i uchun **Private** bo‘lsin. Desktopdagi mavjud **Firewall ruxsati** tugmasini bir marta bosing va Windows administrator so‘rovini tasdiqlang. Bu TCP va UDP 8080 uchun faqat Private/local subnet ruxsatini yaratadi. Muqobil: `desktop/PosElectro.Desktop/allow_firewall.bat`ni administrator sifatida ishga tushiring.

Telefon va desktop bir lokal tarmoqda bo‘lsin. Guest Wi-Fi / AP isolation qurilmalar o‘zaro muloqotini bloklashi mumkin. Broadcast bloklangan tarmoqda avtomatik topish ishlamaydi; yangi IP yoki QR bilan mavjud ulanish yo‘lidan foydalaning. Birinchi ulanish uchun odatdagi QR saqlangan. Qurilma ruxsati o‘chirilsa yoki tokenni o‘z ichiga olmagan eski desktop bazasi tiklansa, yangi QR kerak bo‘lishi mumkin.

Yangi IPni desktopda `ipconfig` orqali tekshiring. Faqat Wi-Fi nomini o‘zgartirish IP haqiqatan almashganini isbotlamaydi.

## Manual tekshirish

| ID | Amal | Kutiladigan natija |
|---|---|---|
| A1 | Birinchi QR ulanishini bajaring; bir mahsulotni o‘zgartiring. | Ikki qurilmada bir xil ma’lumot va Jonli status. |
| A2 | Wi-Fi yoqilgan holda internetni uzing; telefon mobil internetini yoqing. | Lokal savdo/qarz sinxroni ishlaydi. Firebase/CBU natijasi bu test mezoni emas. |
| A3 | Telefondagi Wi-Fi’ni o‘chiring, 30 soniya kuzating. | OFFLINE holati; fon urinishlari Ulanmoqda/Jonli miltillashini keltirmaydi. |
| A4 | Wi-Fi o‘chiq paytda bitta nasiya savdosi va ruxsat etilgan offline qarz to‘lovini kiriting, summalarni yozib oling. Wi-Fi’ni yoqing. | Navbat yetkaziladi; cheklar, to‘lovlar va qoldiq bir marta aks etadi. |
| A5 | Desktop va telefonni boshqa lokal tarmoqqa o‘tkazing. Desktop IPsi haqiqatan o‘zgargan bo‘lsin. QR yoki IP kiritmang. | Ilova ochiq holatda desktop avtomatik topiladi; muvaffaqiyatli syncdan keyin Jonli. Qarz tarixi va qoldiq saqlanadi. Oddiy LANda dastlab 10–30 soniya kuzating; bu qat’iy vaqt kafolati emas. |
| A6 | A5dan oldin offline savdo/to‘lov navbatini yarating. | Yangi IPda navbat yo‘qolmaydi, ikki marta yozilmaydi. Qayta ulanishdan oldingi va keyingi chek/to‘lov GUIDlari bir xil. |
| A7 | Desktopni yoping, telefonda kuting, desktopni yana oching. | Telefon o‘zi qayta ulanadi; UIga qayta kirish yoki QR shart emas. |
| A8 | Yangi IPga avtomatik ulanishdan keyin telefon ilovasini to‘liq yoping va oching. | Saqlangan yangi IP orqali qayta ulanadi. |
| A9 | Shu desktop QR kodini qayta skanerlang, jumladan QRning pairing muddati tugagach. | Saqlangan token amalda bo‘lsa ulanish ishlaydi; tarix/cheklar dublikat bo‘lmaydi. |
| A10 | UDPni vaqtincha bloklang yoki broadcast ishlamaydigan LANda yangi IPni qo‘lda kiriting / yangi QRni skanerlang. | Avtomatik topish bo‘lmasa ham mavjud qo‘lda ulash ishlaydi. UDPni qayta oching. |
| A11 | Narx konfliktini yarating, so‘ng IP almashishini bajaring. | Desktop qayta topiladi, konflikt o‘z-o‘zidan o‘chmaydi; foydalanuvchi tanlovidan keyin sync davom etadi. |
| A12 | Desktopda qurilma ruxsatini bekor qiling. | Telefon avtomatik yangi token olmaydi, yozishlar o‘tmaydi. Yangi QR orqali ulash talab qilinadi. |
| A13 | Alohida test desktopi mavjud bo‘lsa, uni ham tarmoqqa qo‘shing. | Telefon o‘z desktopiga ulanadi; begona serverga offline navbatni uzatmaydi. |
| A14 | Desktop test bazasining eski zaxirasini tiklang; telefon tarixida yangi amallar bo‘lsin. | Token saqlangan bo‘lsa reconnect, so‘ng mavjud epoch recovery ishlaydi. Token tiklanmagan bo‘lsa QR talab etiladi. Telefon navbati yo‘qolmasin; balans, chek, to‘lov va qoldiqda dublikat bo‘lmasin. |
| A15 | Desktop sync serverini tez to‘xtatib-yoqing, keyin IPni almashtiring. | TCP va discovery qayta ishlaydi; UDP port bandligi sabab avtomatik topish o‘chib qolmaydi. |

Har bir moliyaviy holatda ikki qurilmada mijoz balansi, to‘lov voqealari soni, chek raqami va ombor qoldig‘ini solishtiring. Test ma’lumotini haqiqiy mijoz bazasiga qo‘shmang.

Xato chiqsa: test ID, eski/yangi IP, Windows tarmoq turi, firewall ruxsati natijasi, ekrandagi xabar, voqea vaqti hamda ikki qurilmadagi summalarni yozing. Token yoki QRning ulash kodini log/screenshot bilan tarqatmang.

## Bajarilgan tekshiruv chegarasi

Faqat o‘zgargan kod yo‘llari ko‘rib chiqildi va `git diff --check` bajarildi. Build, avtomatik test, emulator va real qurilma testlari bajarilmadi. Yuqoridagi jadval hali PASS deb hisoblanmaydi. `main`ga merge va yangi APK/EXE chiqarish bajarilmadi.

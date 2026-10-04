# Moliyaviy hisob, chek va backup tuzatishlarini sinash

Branch: `fix/business-integrity`. Telefon va desktopni birga yangilang. Firebase litsenziyalash va CBU kurs olish xizmati o‘zgartirilmagan. Manfiy ombor qoldig‘i ruxsat etilgan.

## Hisoblash siyosati

Yangi savdoda kurs, mahsulot nomi, kategoriya va birlik saqlanadi. So‘mdagi foyda = tarixiy tushum − tarixiy tannarx − karta xarajati. Dollar foydasi har savdoning tarixiy kursida hisoblanib, keyin yig‘iladi. Brak jami foydani kamaytiradi, ammo oddiy savdolar soni va sotilgan tovarlar soniga kirmaydi; alohida brak soni va tannarxi bor.

Eski chekda kurs bo‘lmasa, USD tannarxli qatorlar va saqlangan jami tannarxdan kursni chiqarish mumkin bo‘lgan holatda tiklanadi. Bu o‘sha chek uchun samarali tarixiy kursdir. Faqat UZS tannarxli eski chekda kursni bilib bo‘lmaydi: USD jami noma’lum deb ko‘rsatiladi. Avval saqlanmagan kategoriya “Tarixiy kategoriya noma’lum” bo‘ladi. Eski so‘m summalariga tegilmaydi.

Hisobotning qatorlari, soliq va tannarx bir xil 0,01 so‘mlik taqsimot qoidasi bilan hisoblanadi; kategoriya/ombor eksportlari qo‘shilganda umumiy summani beradi. Soliq qatorlar tushumiga mutanosib taqsimlanadi. Bu biznesning ijara/ish haqi kabi boshqa xarajatlarini hisobga olmaydi.

## Tekshirish

1. Test bazada tannarxi $10, narxi 150 000 so‘m bo‘lgan tovar yarating; savdo paytidagi kursni yozib oling. 1 dona karta orqali 1,8% xarajat bilan soting. Kutilgan foyda: `150000 − 10 × kurs − 2700`.
2. Telefon va desktopni sinxronlang. Ikkalasidagi foyda va Excel natijalari tengligini tekshiring. Keyingi kurs yangilanishi eski foydani o‘zgartirmasligi kerak.
3. Bir chekda turli kategoriya va omborlardan tovar soting. Har filtr ostidagi jami faqat shu qatorlardan iborat bo‘lsin; chekni ochganda esa uning to‘liq tarkibi ko‘rinsin.
4. Tovarni qayta nomlang va kategoriyasini almashtiring. Oldingi chek va Excelda eski nom/kategoriya saqlansin.
5. Telefonning “Tasdiqlash va Sotish” tugmasini tez-tez bosing. Bitta chek, bitta qoldiq yechimi bo‘lsin. Xatolikdan keyin tugma yana ishlashi kerak.
6. Chek raqamini telefondan desktopga va aksincha ko‘chirib qidiring. `LP-…` raqami bir xil bo‘lsin. Bugun filtrida eski chek raqamini qidirganda eski chek ham chiqsin. Qidiruvni tozalaganda sana filtri qaytsin. Qisqa eski raqamlar faqat lokal ID qidiruvi; bir necha natija bo‘lsa sana/summa bilan solishtiring.
7. Oddiy savdo va brak yarating. Barchasi/Savdo/Brak filtrlarini hamda eksportni solishtiring; brak oddiy savdolar soniga qo‘shilmasin.
8. Qoldiq 0 bo‘lgan tovarni soting. Manfiy qoldiq saqlansin; telefon/desktopda savdo yo‘qolmasin.
9. Yangi savdo qilmasdan tovar nomini yoki ombor qoldig‘ini o‘zgartiring. Telegram backupni bajaring. Fayl yuborilsin va nusxada bu o‘zgarish bo‘lsin.
10. Faqat test bazada backupni qayta tiklang. Oldindan mavjud bazaning qaytish nusxasi ilovaning ichki `restore-recovery` papkasida saqlanadi. Noto‘g‘ri/shikastlangan fayl mavjud bazaga tegmasdan rad etilsin.

Telegramga real jo‘natish internetni talab qiladi. Wi-Fi savdo sinxroni internet serverisiz ishlaydi. Avtomatik backup Android tarmoq/batareya cheklovlari sabab belgilangan daqiqadan kechikishi mumkin.

## Avtomatik tekshiruvlar

- `dotnet run --project tests/WifiSync.CoreTests`: LAN sinxroni va moliyaviy hisoblash.
- `dotnet run --project tests/Business.CoreTests`: haqiqiy desktop SQLite migratsiyasi, qidiruv, tarixiy nom/kategoriya/kurs, filtrlar va takroriy GUID.
- `python -m unittest discover -s tests -p 'test_*.py' -v`: aynan jo‘natiladigan SQLite triggerlari.
- `bash gradlew :app:assembleDebug :app:assembleDebugAndroidTest`: Android kompilyatsiyasi.
- `bash gradlew :app:connectedDebugAndroidTest`: Room, moliyaviy hisob va nusxa olish sinovlari; API 26 va 35.

## Qaytarish va bekor qilish

Hisobotdan `LP-…` chekni oching → **Qaytarish**. Miqdor, qabul ombori, yaroqli/nuqsonli holat, sabab va naqd/karta summasini kiriting. Karta xarajati faqat haqiqatan qaytarilgan bo‘lsa yoziladi; odatiy qiymat `0`.

Telefonda **Loyihani saqlash** oflayn ishlaydi (oldin desktopga juftlangan bo‘lishi kerak). Yakuniy tasdiq mahalliy desktopdan olinadi. Internet kerak emas, desktop yoqilgan va bir LAN tarmog‘ida bo‘lishi kerak. Oflayn miqdor oxirgi sinxron holatini ko‘rsatadi; desktop yakuniy tekshiradi.

Tasdiq uzilib qolsa yangi so‘rov yaratmang: **Natijani qayta tekshirish** ni bosing. Bir xil so‘rov yana yuborilganda yangi pul/ombor yozuvi yaratilmaydi. Telefon yoki desktop qayta ishga tushganda loyiha/so‘rov saqlanadi. Tasdiq oynasi bankdan avtomatik pul o‘tkazmaydi.

Tasdiqlanganda `RT-…` chek yaratiladi. Asl savdo o‘z sanasi bilan qoladi; qaytarish qaytarilgan sananing foydasiga ta’sir qiladi. Xato qaytarishni `RT-…` tafsilotidagi **Qaytarishni bekor qilish** orqali bekor qiling. `RV-…` teskari yozuv yaratiladi; asl tarix o‘chirilmaydi. Jismoniy tovar va pulni ham mos ravishda kelishtiring.

### Ikki qurilmada qabul sinovi

1. Kurs 12 000, sotuv 150 000, tannarx $10, karta xarajati 2 700: foyda 27 300. To‘liq yaroqli qaytish, komissiya qaytmasa: savdo+qaytarish foydasi **−2 700**. Nuqsonli qaytishda **−122 700**; sotiladigan qoldiq oshmasin.
2. 0,7 metr, jami 100 so‘m, jami tannarx 33,33 so‘m: yetti marta 0,1 metrdan yaroqli qaytaring. Jami qaytarish 100, tannarx tiklanishi 33,33; sakkizinchi qaytarish rad etilsin.
3. Bir mahsulotni bir chekda ikki narx/ombor bilan soting. Faqat tanlangan qator qaytsin. Kurs, nom va narxni o‘zgartirib qaytaring — eski qiymatlar ishlasin.
4. Telefon va desktopdan bir chekning qolgan miqdorini bir paytda qaytaring. Tasdiqlangan jami miqdor sotilgandan oshmasin.
5. Telefon tasdiqlash paytida Wi‑Fi’ni uzing, ilovani qayta oching va natijani qayta tekshiring. Bitta `RT-…`, bitta qoldiq o‘zgarishi bo‘lsin.
6. `RT-…` ni bekor qiling; bitta `RV-…`, teskari ombor va foyda ta’siri bo‘lsin. Qayta bekor qilish yangi operatsiya yaratmasin.
7. Barchasi/Savdo/Qaytarish/Brak filtrlari va Excel summalari teng bo‘lsin. Qaytarish va bekor qilish oddiy savdo soniga kirmasin. Boshqa kunda qaytarganda asl sana qayta yozilmasin.
8. Yuborilgan, ammo javobi olinmagan qaytarish bilan backup oling, test qurilmaga tiklang va o‘sha desktopga ulang. So‘rov raqami saqlansin; qayta tekshirish yangi qaytarish yaratmasin.
9. `LP-…`, `RT-…`, `RV-…` raqamlarini ikkala qurilmada qidiring; bir xil hodisa topilsin. Qaytarish chekini chop etishda asl `LP-…` raqami ko‘rinsin.

Migratsiyadan oldingi nusxa telefonda ichki `migration-backups`, desktopda baza yonidagi `Backups` papkasida saqlanadi. Eski `refunds` yozuvi bor cheklar taxminiy hisobdan saqlash uchun qo‘lda tarix tekshiruvi talab qiladi.

GitHub CI Android API 26/35 emulyatorlarini, desktop build, haqiqiy SQLite va lokal HTTP sinovlarini ishlatadi. Yuqoridagi jismoniy telefon–Windows/printer/Telegram qabul sinovlari avtomatik CI bilan bir xil tekshiruv emas.

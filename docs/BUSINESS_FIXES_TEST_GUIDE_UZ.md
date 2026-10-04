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

Qaytarish funksiyasi hozircha reja: [RETURNS_PLAN_UZ.md](RETURNS_PLAN_UZ.md).

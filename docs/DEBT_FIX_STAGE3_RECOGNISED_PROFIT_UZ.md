# 3-bosqich — nasiya foydasini kelgan pulga mutanosib tan olish

Branch: `fix/debt-stage3-complete`. Asos: `ec70b1e` (1, 2A va 2B birga).

## Holat

Kod va o‘zgargan yo‘llar ko‘rib chiqildi. **Build, CI, emulator va avtomatik testlar bajarilmadi; APK/EXE yaratilmagan.** Quyidagi jadval kutiladigan natijalar, o‘tkazilgan test emas. Main merge yo‘q.

Hisoblash Kotlin va C#da bir xil qoida bilan yozilgan. Moliyaviy tarix va sinxron paketlar o‘zgartirilmaydi; migratsiya yo‘q. UI joylashuvi, Firebase, CBU va manfiy qoldiq qoidasi saqlangan.

## Hisoblash qoidasi

Nasiya bo‘yicha qolgan tovarlar marjasi olingan pul ulushiga mutanosib tan olinadi. Oldin qaytarilgan tovarlarning sotuv qiymati/tannarxi ajratiladi; qarzning musbat qoldig‘i olinmagan pulni bildiradi. Kredit tufayli tan olingan foyda to‘liq marjadan oshmaydi.

Har bir asl mahsulot qatori uchun:

- qolgan sotuv qiymati = asl sotuv qiymati − qaytarilgan sotuv qiymati;
- qolgan tannarx = asl tannarx − qaytarilgan tovar tannarxi;
- pul bilan qoplangan ulush = (qolgan jami sotuv − musbat qarz qoldig‘i), 0 va qolgan jami sotuv oralig‘ida cheklanadi;
- tan olingan marja = qolgan marja × pul bilan qoplangan ulush / qolgan jami sotuv;
- nuqsonli qaytarishning omborga tiklanmagan tannarxi haqiqiy zarar sifatida ayriladi;
- boshlang‘ich va keyingi karta komissiyalari haqiqiy xarajat sifatida ayriladi. Ular tushum nisbatiga ikkinchi marta ko‘paytirilmaydi.

Hisobotda har bir vaqtdagi yig‘ilgan natijaning oldingi natijadan farqi ko‘rsatiladi. Shu sabab kechagi nasiya bugun to‘lansa, bugungi foyda oshadi; kechagi savdo summasi qayta yozilmaydi. To‘lov bekori, kredit o‘tkazish, kreditni naqd qaytarish, mahsulot qaytarish/bekori ham imzoli yozuvlar orqali hisoblanadi.

Mahsulotlar o‘rtasida tannarx/marja turlicha bo‘lsa, qaytarish qolgan savdoga tegishli tan olingan marjani qayta taqsimlashi mumkin. Bu o‘zgarish qaytarish sanasiga kiradi. Bir xil millisekunddagi yozuvlar bir holat sifatida birga hisoblanadi.

UZS hisoblari tiyinlarda, tekshiriladigan integer arifmetika bilan yuritiladi. Komissiya taqsimotining yig‘indisi asl imzoli komissiyaga teng qoladi. USD marja asl savdoning tarixiy kursi bilan, keyingi komissiya o‘sha to‘lovning saqlangan kursi bilan hisoblanadi. Kerakli eski kurs yo‘q bo‘lsa USD to‘liq deb ko‘rsatilmaydi; joriy CBU kursi taxmin sifatida qo‘yilmaydi.

## Ekran va Excel

- Oddiy savdo/brak hisoblari saqlanadi. Nasiya va unga bog‘liq qaytarishlarning foydasi yangi hisobdan keladi.
- Asl chekdagi foyda — tanlangan davr oxirigacha shu chekka yozilgan tan olingan foyda. Qaytarish ta’siri alohida RT/RV chekiga tegishli; asl chekdan yana ayirib, ikki marta hisoblanmaydi.
- Davr kartasi faqat shu davrdagi foyda o‘zgarishlarini yig‘adi. Shuning uchun kechagi chekning bugungi to‘lovi bugungi kartaga kiradi, lekin bugungi savdolar sonini oshirmaydi.
- Excelda keyingi to‘lov/foyda o‘zgarishi uchun asl mahsulot va chek raqami bilan **miqdori, savdo summasi, tannarxi, naqd/kartasi 0 bo‘lgan foyda qatorlari** bo‘lishi mumkin. Bular yangi sotuv emas. Sana — foyda o‘zgarishi sanasi.
- Kategoriya/ombor bo‘yicha foyda asl chekdagi tarixiy mahsulot va omborga taqsimlanadi. “Savdo” to‘lov orqali tan olingan foydani, “Qaytarish” RT/RV ta’sirini qamrab oladi.
- Chek qidiruvi mavjud xulqqa muvofiq butun tarixdan izlaydi; Excel foyda qatorlari ham shu qidiruv bilan filtrlanadi.
- Qarz hisobi yoki qaytarish tarixi hisobga mos kelmasa “Hisob mavjud emas”/xato xabari chiqadi; noto‘g‘ri hisobotni eksport qilish bloklanadi. Bunday holatda tarixni o‘chirmang — sinxron/backupni tekshirish kerak.

## Yuklab olish

Ikkala dasturni shu branchning oxirgi commitidan build qiling. Saqlanmagan lokal kodga tegmaslik uchun yangi papka:

```powershell
git clone --branch fix/debt-stage3-complete --single-branch https://github.com/akkiS18/LinePOS.git LinePOS-profit-test
cd LinePOS-profit-test
git log -1 --oneline
```

Alohida kod papkasi alohida baza degani emas: desktop va telefon bazalarini zaxiralang, test nusxasida ishlang. Test vaqtida tarixiy kurs/komissiyani yozib oling. Barcha qiymatlar quyida so‘mda.

## Asosiy manual testlar

Har bir mustaqil ssenariyni yangi test mijoz/chek bilan boshlang. Har qadamda mobil, desktop, hisobot kartasi va Excelni solishtiring. Yangilash tugmasini bosib, sinxron tugaganini tekshiring.

| № | Ssenariy | Kutiladigan foyda |
|---|---|---|
| 1 | Tannarx 70 000, sotuv 100 000, to‘liq nasiya, komissiya 0. | Boshlang‘ich foyda 0; 30 000 ko‘rinmasin. |
| 2 | 1-test qarziga 20 000 naqd to‘lov. | Shu to‘lov sanasida +6 000. Asl chek yig‘ilgan foydasi 6 000. |
| 3 | Yana 30 000 naqd, so‘ng 50 000 naqd. | Mos ravishda +9 000 va +15 000; yig‘ilgan foyda 30 000. |
| 4 | Alohida 100 000lik/70 000 tannarxli nasiya, boshlang‘ich 20 000 naqd. | Savdo paytida 6 000. Qolgan 80 000 kelganda +24 000. |
| 5 | 20 000 to‘lovni bekor qilish (boshqa to‘lov yo‘q). | Bekor qilingan kunda −6 000; yig‘ilgan foyda 0. |
| 6 | Wi-Fi uzib ikkala qurilmada alohida to‘lov bilan ortiqcha kredit hosil qiling. | Kreditning o‘zi foydani 30 000dan oshirmaydi. Kreditni naqd qaytarish ham to‘liq qoplangan savdo marjasini takror kamaytirmaydi. |
| 7 | Kreditning bir qismini boshqa nasiya hisobiga o‘tkazing. | Manba to‘liq qoplanganligicha qolsa marjasi kamaymaydi; maqsad hisobning foydasi unga tegishli marja ulushiga oshadi. Yangi naqd tushum yaratilmaydi. |
| 8 | 100 000/70 000 savdo to‘liq nasiya va umuman to‘lanmagan. Tovarni to‘liq, sotishga yaroqli qilib qaytaring. | Yig‘ilgan foyda 0. Soxta −30 000 zarar paydo bo‘lmasin. |
| 9 | Shu savdoga 20 000 to‘lab, keyin to‘liq yaroqli qaytarish. | Avval +6 000; RT ta’siri −6 000; umumiy natija 0. |
| 10 | To‘liq to‘langan 100 000/70 000 savdoni yaroqli qaytaring. | Avval 30 000; RT ta’siri −30 000; umumiy 0. |
| 11 | To‘lanmagan 100 000/70 000 savdoni to‘liq nuqsonli qaytaring. | Tannarx qaytmagani uchun −70 000 zarar. |
| 12 | 9 yoki 11-test qaytarishini bekor qiling. | Tegishli qaytarish ta’siri aynan qarama-qarshi qaytadi; ombor/qarzda ikkinchi takror yozuv bo‘lmasin. |
| 13 | 20 000 to‘langan 100 000/70 000 savdoning bir xil marjali 30 000lik qismini yaroqli qaytaring. | Qarz 80 000dan 50 000ga; qolgan 70 000 savdoning 20 000i qoplangan. Yig‘ilgan marja 6 000 bo‘lib qoladi. |

## Komissiya, USD va filtrlar

| № | Ssenariy | Kutiladigan natija |
|---|---|---|
| 14 | 100 000/70 000 savdo kursi 10 000. Keyingi 20 000 karta to‘lovi, 400 komissiya, to‘lov kursi 12 000. | UZS +5 600. USD: 6 000/10 000 − 400/12 000 = taxminan 0.5666667; ekranda 0.57. Komissiya yana umumiy kartadan ayirilmasin. |
| 15 | 14-test to‘lovini bekor qiling, CBU kursini o‘zgartiring. | −5 600 va taxminan −0.5666667. Joriy kurs tarixiy natijani o‘zgartirmasin. |
| 16 | Ikki mahsulot/kategoriya/omborli bitta nasiya; qisman to‘lov va komissiya. | Filtrlardagi qator foydalari yig‘indisi umumiy foydaga teng; komissiya filtrdan yo‘qolmasin yoki ikki marta ayrilmasin. |
| 17 | Savdo bir kun, to‘lov boshqa kun; har kuni va ikkala kunni birga oling. | Foyda o‘z sanasida; ikkala davr yig‘indisi birgalikdagi davrga teng. To‘lov yangi sotuv/mahsulot sonini oshirmasin. |
| 18 | To‘lov sanasida yangi savdo yo‘q. Excel oling. | Foyda mavjud, savdolar soni 0. Nol miqdorli foyda qatorlari asl mahsulot nomini saqlaydi. |
| 19 | Asl LP chekni, keyin RT/RV chekni qidiring; ekran va Excelni solishtiring. | Faqat tegishli chek qatorlari. Asl chek va qaytarish foydasi ikki marta ayirilmaydi. |
| 20 | Yetishmaydigan tarixiy FX bo‘lgan test yozuvi. | UZS hisoblanadi; USD to‘liq deb ko‘rsatilmaydi. |
| 21 | Brak filtri va oddiy naqd/karta savdosi. | Avvalgi qoidalar saqlanadi; nasiya foyda qatorlari brak/savdo sonini buzmaydi. |
| 22 | Ko‘p tarixli test baza, so‘ng bir xil ma’lumotni qayta-qayta sinxron qiling. | Natija o‘zgarmaydi. Hisobot ochilish/yangilanish vaqtini qayd qiling; sust qurilmada sezilarli kechikish bo‘lsa bildiring. |

Lokal agent uchun: faqat test nusxasida qarz hisobi/qaytarish bog‘lanishi yetishmagan holatni yarating. Crash yoki taxminiy foyda o‘rniga xabar bo‘lsin, eksport bloklansin. Bir millisekunddagi bog‘liq yozuvlarni turlicha import tartibida kiriting: yakuniy foyda bir xil bo‘lsin.

Natijalar qayd etilmaguncha productionga tayyor deb hisoblamang. Qolgan 4-bosqich — IP o‘zgarganda avtomatik topish/qayta ulanish.

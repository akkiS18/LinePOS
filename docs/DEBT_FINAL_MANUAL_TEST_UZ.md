# Qarz daftari va Wi-Fi: uch bosqich uchun yakuniy manual test

## Holat

Test branch: `fix/debt-stage3-complete`. 1-bosqich hisoblashlari, 2-bosqich qarz amallari va 3-bosqich ulanish/qoldiq o‘zgarishlari birga. Main bilan birlashtirilmagan.

3-bosqich kodi ko‘rib chiqildi, lekin **build, CI, emulator va avtomatik testlar bajarilmadi**. Tayyor APK/EXE yaratilmagan. Quyidagi natijalar kutiladigan natijalar, o‘tkazilgan test hisoboti emas. Productionga tayyorligi manual tekshiruvdan keyin baholanadi.

UI joylashuvi, Firebase, CBU va manfiy ombor qoldig‘iga ruxsat o‘zgarmadi. Qarz qoldig‘i va tushum raqamlari tuzatildi; telefondagi qarz hisobida chek raqami desktopdagi kabi to‘liq GUIDdan olinadi.

## Yuklab olish

Push qilinmagan ishlaringizga tegmaslik uchun alohida papka oching:

```powershell
cd "$HOME\Documents"
git clone --branch fix/debt-stage3-complete --single-branch https://github.com/akkiS18/LinePOS.git LinePOS-test-final
cd LinePOS-test-final
git log -1 --oneline
notepad docs\DEBT_FINAL_MANUAL_TEST_UZ.md
```

Desktop va telefon dasturlarining ikkalasini ham shu branchdan odatdagi lokal usulingizda build qiling. Alohida kod papkasi alohida baza degani emas: avval ikkala bazani zaxiralang va test nusxalarida ishlang. Build xatosi bo‘lsa, to‘liq xabarini yuboring; eski APK/EXE bilan testni davom ettirmang.

## 1. Avvalgi bosqichlar

1. `docs/DEBT_STAGE1_ACCOUNTING_UZ.md` dagi hisoblash/komissiya testlarini bajaring. Yuklab olishda eski branch o‘rniga shu yakuniy branchni ishlating.
2. `docs/DEBT_STAGE2_MANUAL_TEST_UZ.md` dagi 1–14 testlarni bajaring. Endi reversal/refund/transferdan keyin ekrandagi qoldiq ham kutilgan qiymatga teng bo‘lishi shart.
3. `docs/DEBT_STAGE2A_CUSTOMER_SYNC_UZ.md` dagi mijoz tahriri va konflikt testlarini bajaring.

Mobil UI’da bo‘lmagan reverse/refund/transfer tugmalari qo‘shilmadi. Ularning servis/API tekshiruvlarini 2-bosqich qo‘llanmasidagi texnik tartibda lokal agent bajara oladi.

## 2. Birinchi ulanish va qayta ulanish

| № | Amal | Kutiladigan natija |
|---|---|---|
| A1 | Yangi test telefon bazasini yangi test desktop bazasiga ulang. | Qarz sxemasi yoki immutable trigger xatosi yo‘q; mijoz yaratish va qarz sinxroni ishlaydi. |
| A2 | Boshqa toza test telefon bazasida hali ulanmasdan mahsulot va 100 dona qoldiq, mijoz yarating; 3 dona to‘liq nasiya soting. Keyin bo‘sh test desktopga birinchi marta ulang. | Mahsulot, mijoz, savdo va qarz bir marta yetadi. Ikkalasida 97 dona; 94 yoki 100 emas. Telefonning tarixiy GUIDlari saqlanadi. |
| A3 | A2 da ulanishdan oldin mijoz kontaktini tahrirlang, qarzning bir qismini to‘lang. | Kontaktning oxirgi qiymati, savdo va to‘lov saqlanadi; summa va qoldiq teng. |
| A4 | Wi-Fi ni 3 marta uzib ulang, ikkala ilovani yoping va qayta oching. | Saqlangan ulanish bilan qayta ulanadi; har safar kod kiritish talab qilinmaydi. Dublikat yo‘q, navbat bo‘shaydi. |
| A5 | Desktop IP manzilini o‘zgartiradigan tarmoqqa o‘ting; mavjud ulash oqimi orqali shu desktopni toping. | Shu baza yangi IPda ishlaydi; qarz tarixi o‘zgarmaydi. |
| A6 | Ilovadagi aloqani uzish funksiyasidan foydalanib, shu desktopning yangi QR/kodi orqali qayta ulang. | Lokal qarz tarixi o‘chmaydi, summalar va qoldiq takror qo‘llanmaydi. Bu ataylab ulanish kalitini olib tashlash bo‘lgani uchun yangi QR/kod kerak. |
| A7 | Test nusxasida qarz tarixi bor telefonni mutlaqo boshqa do‘kon bazasiga bog‘lashga urining. | Moliyaviy tarix boshqa do‘kon nomiga yashirin ko‘chirilmaydi; tushunarli xato, eski ma’lumot saqlanadi. Bu kundalik bitta kompyuterga qayta ulanish holati emas. |

## 3. Ombor va qarz qoldig‘i

Har bir qatorni alohida sinov mahsuloti/mijozida boshlang. Internet o‘chiq, lokal Wi-Fi yoqilgan holatda ham sinang.

| № | Amal | Kutiladigan natija |
|---|---|---|
| B1 | Desktopda telefonda hali yo‘q mahsulotga 100 dona kiriting, 3 dona nasiya soting; keyin telefonni ulang. | Telefonga mahsulot kechroq kelganida ham yakuniy qoldiq 97. Qarz paketining kech qo‘llanishi yana 3 dona ayirmaydi. |
| B2 | Telefonda 100 donadan 3 dona offline nasiya soting, qayta ulang. | Ikkalasida 97 dona. Yana uzib ulash natijani o‘zgartirmaydi. |
| B3 | Ko‘p mahsulotli bazada sinxron ketayotganda telefonda yana 2 dona nasiya soting. | Yangi lokal savdo kelayotgan server qoldig‘i bilan yo‘qolmaydi; barcha amallar yetgach boshlang‘ich qoldiq minus jami sotilgan miqdor. Bu vaqtga bog‘liq holatni lokal agent tarmoq javobini kechiktirib aniqroq tekshirishi mumkin. |
| B4 | 100 000 so‘m qarz; 20 000 to‘lov; desktopda shu to‘lovni bekor qilish. | Ketma-ket qoldiq 100 000 → 80 000 → 100 000. Telefon ro‘yxati, mijoz tafsiloti va jami ko‘rsatkichi mos. |
| B5 | To‘lanmagan 100 000 qarzdan 30 000lik mahsulotni qarzga hisoblab qaytaring; qaytarishni bekor qiling. | Qarz 100 000 → 70 000 → 100 000. Ombor bir marta ortadi va bir marta kamayadi. “To‘langan” pul tushumi qaytarish sabab oshmaydi. |
| B6 | 2-bosqichdagi ortiqcha kredit, kredit o‘tkazish va naqd qaytarish testlarini bajaring. | Musbat va manfiy deltalar hisobga olinadi. Hisoblar orasidagi o‘tkazish umumiy balans/tushumni o‘zgartirmaydi. Naqd kredit qaytarish net tushumni kamaytiradi. |
| B7 | Muddati o‘tgan qarzning to‘lovini bekor qiling. | Qarz qayta ochiladi; muddati o‘tganlar filtri va hisoblar soni qayta to‘g‘ri ko‘rinadi. |
| B8 | Bitta qarz chekining raqamini ikkala qurilmada solishtiring va mavjud chek qidiruvida toping. | Bir xil to‘liq chek raqami, bir xil savdo. |

“To‘langan” net qarz tushumi: payment minus payment_reversal minus credit_refund. Return_offset va credit_transfer pul tushumi emas; ular balansga ta’sir qiladi. Savdodagi boshlang‘ich naqd/karta tushumi savdo hisobotida alohida qoladi.

## 4. Bir xil do‘kon bazasini tiklash

Faqat test nusxasida bajaring. Ikkala qurilmaning tiklashdan oldingi zaxirasini saqlang.

1. Mahsulot, mijoz, qarz savdosi ikkala qurilmada mavjud va navbat bo‘sh bo‘lsin. Desktop zaxirasini oling.
2. Telefonda 20 000 so‘m qarz to‘lovini bajaring, desktopga yetishini kuting.
3. Telefon ulangan holda desktopda 1-qadamdagi zaxirani tiklang. Faol sinxron so‘rovlari tugatilgandan keyin tiklash bajarilishi kerak. Qayta ulanishni kuting. Zaxirada telefonning avtorizatsiyasi yo‘q bo‘lsa, qayta QR/kod bilan ulang.
4. Telefonda saqlangan to‘lov desktopga qayta yetishi kerak; bitta payment, bir xil qoldiq. Ilovalarni qayta ochib ham tekshiring.
5. Tiklashdan keyin yangi to‘lov va mahsulot qaytarish bajaring. Eski qurilma ketma-ketligi bilan konflikt va dublikat bo‘lmasin.
6. Alohida testda zaxiradan keyin yangi nasiya savdosi qiling; mahsulot zaxirada avvaldan bo‘lsin. Tiklash va sinxrondan keyin qarz va ombor qoldig‘i ikkala qurilmada bir xil bo‘lsin.
7. Tiklash davomida tiklash buyrug‘ini takror bosish ikkinchi parallel tiklashni boshlamasin.

**Chegara:** telefon butun desktop bazasining o‘rnini bosadigan zaxira emas. Eski zaxirada mahsulot yoki qaytarish sarlavha/qatorlari yetishmasa, ularga bog‘liq qarz paketi kutishda qolishi mumkin. Bu holat avtomatik to‘liq tiklandi deb hisoblanmaydi. Navbatni yoki tarixni qo‘lda o‘chirmang; mos to‘liq zaxirani tiklash yoki ma’lumotlarni alohida yarashtirish kerak. Ushbu bosqich yetishmagan biznes hujjatlarini taxminan yaratmaydi.

## Natijani qayd qilish

Har bir test uchun: ID, desktop/telefon versiyasi (commit), kutilgan va haqiqiy summa/qoldiq, sinxron xabari, navbat soni, xato matni/skrinshot. Hisoblashda tafovut yoki navbatda doimiy kutish bo‘lsa, keyingi production merge to‘xtatiladi.

Hammasi muvaffaqiyatli bo‘lmaguncha main bilan birlashtirmang. Bu branch uchun PR/CI ochilmagan; testni o‘zingizga qulay paytda boshlashingiz mumkin.

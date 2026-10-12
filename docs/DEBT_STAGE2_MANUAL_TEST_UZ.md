# 2-bosqich — qarz amallari va Wi-Fi sinxron: manual test

## Holat

Test branch: `fix/debt-stage2-complete`. Unda 1-bosqich, 2A mijoz tahrirlari va 2-bosqichning qolgan tuzatishlari birga bor. Main bilan birlashtirilmagan.

Kod va diff ko‘rib chiqildi; **build, CI, emulator yoki avtomatik testlar bajarilmadi**. APK/EXE yaratilmagan. Quyidagi tekshiruvlarning natijasi hali noma’lum.

UI joylashuvi o‘zgartirilmadi. Mavjud xabarlar orqali navbat, rad etilgan so‘rov va oldingi saqlangan to‘lov haqida ma’lumot beriladi. Firebase, CBU va manfiy ombor qoldig‘iga ruxsat o‘zgarmadi.

## Koddagi o‘zgarishlar

- Mijoz kontakt tahriri/blok holati lokal o‘zgarish bilan bir tranzaksiyada offline navbatga tushadi. Bir maydon konflikti jimgina ustidan yozilmaydi.
- `payment_reversal`, `credit_refund`, `credit_transfer`, `return_offset`, `return_reversal` yozuvlari ham qarz transportiga qo‘shildi. V1 savdo/to‘lov paketlari formati o‘zgarmadi; yangi turlar V2 format va capability bilan uzatiladi.
- Asl to‘lov, hisob yoki qaytarish yozuvi kelmagan paket durable inboxda kutadi. Qaytarishning qarz oqibati asl qaytarish summalari bilan tekshiriladi; uning stock ta’siri ikkinchi marta qo‘llanmaydi.
- Eski desktop qaytarishlarining navbatsiz qolgan qarz yozuvlari server ishga tushganda jurnalga kiritiladi. Immutable moliyaviy tarix qayta yozilmaydi.
- Tarmoqdagi pul amallari yuboruvchi saqlagan RequestGuid/OccurredAt bilan bajariladi. Bir xil request qayta yuborilsa yangi to‘lov/qaytarish yaratilmaydi.
- Telefonning pul so‘rovlari server identifikatori va o‘zgarmas body bilan saqlanadi. Noma’lum natija o‘chirilmaydi; aniq rad etilgan natija ham foydalanuvchi/chaqiruvchi ko‘rishi uchun saqlanadi.
- Oddiy qarz to‘lovining lokal urinish IDsi, summalari, komissiyasi va kursi saqlanadi. Saqlangandan keyin ilova yopilsa, mijozning to‘lov oynasi qayta ochilganda oldingi natija aniqlanadi.
- Desktopda yopilgan to‘lov/qaytarish oynasining takror tasdiqlashi rad etiladi; telefonda to‘lov targeti yuborishdan oldin olinadi.
- Qarzni pull qilishda guruhlar cursor bilan bir xil ketma-ketlikda olinadi. Faqat yuborilgan GUID uchun kelgan ACK qabul qilinadi.

## Yuklab olish

Mavjud checkout va push qilinmagan o‘zgarishlarga tegmaslik uchun alohida papka:

```powershell
cd "$HOME\Documents"
git clone --branch fix/debt-stage2-complete --single-branch https://github.com/akkiS18/LinePOS.git LinePOS-test-2
cd LinePOS-test-2
notepad docs\DEBT_STAGE2_MANUAL_TEST_UZ.md
```

Ikkala dasturni ham shu branchdan odatdagi lokal usulingizda build qiling. Alohida kod papkasi alohida baza yaratmaydi: bazani zaxiralang va test nusxasini ishlating. Dastlab avvaldan ulangan desktop/telefon juftligida sinang.

## Tartib

Har bir testda telefon va desktopdagi amal tarixi, summalar, dublikatlar va sinxron xabarini solishtiring. Internetni o‘chirib, lokal Wi-Fi ni qoldirish mumkin.

| № | Amal | Kutiladigan natija |
|---|---|---|
| 1 | Sinov mijozini yarating va 100 000 so‘mlik to‘liq nasiya savdo qiling. | Ikkala qurilmada bir mijoz va bir nasiya hisob paydo bo‘ladi. |
| 2 | Telefonda 20 000 so‘m naqd qarz to‘lovini tasdiqlashni tez ikki marta bosing. | Bitta 20 000 so‘mlik payment yozuvi. Qarzning matematik qoldig‘i 80 000. |
| 3 | Wi-Fi ni uzib telefonda yana 10 000 so‘m to‘lang. Ilovani yoping, qayta oching va ulang. | To‘lov saqlanib qoladi va desktopga bir marta yetib boradi. Matematik qoldiq 70 000. |
| 4 | Aloqani qayta-qayta uzib ulang, ikkala dasturni qayta oching. | Oldingi to‘lovlar qayta yaratilmaydi. Navbat yakunda bo‘shaydi. |
| 5 | Desktopda 20 000 so‘mlik birinchi to‘lovni bekor qiling. | Telefonga bitta payment_reversal keladi, asl payment bekor qilingan deb belgilanadi. Matematik qoldiq 90 000. Yakuniy branchda ekrandagi qoldiq ham shu qiymat bo‘lsin. |
| 6 | Bir xil bekor qilishni yana urining. | Ikkinchi reversal yozuvi yaratilmaydi. |
| 7 | Shu savdoning 30 000 so‘mlik mahsulotini desktopdan qaytaring. Hali qarz borligi uchun shu qismi qarzdan chegirilsin. | Bitta return_offset; qaytarish cheki, qatori va qarz yozuvi ikkala qurilmaga yetadi. Matematik qoldiq 60 000. Omborga mahsulot faqat bir marta qaytadi. |
| 8 | Desktopda shu qaytarishni bekor qiling. | Bitta return_reversal keladi. Matematik qoldiq 90 000; qaytarilgan stock bir marta ortga olinadi. |
| 9 | Aloqa uzilganida desktopda bekor qilish/qaytarish bajaring; so‘ng telefonni ulang. | Yetishmagan asl yozuvlar kelgach bog‘liq amallar ham qo‘llanadi. Tarix yo‘qolmaydi, dublikat bo‘lmaydi. |
| 10 | Bir mijozga ikkinchi nasiya savdo yarating. Test nusxasida ikki qurilma offline bir hisob qarzini alohida to‘lab, sinxrondan keyin kredit hosil qiling. | Har bir haqiqiy alohida payment saqlanadi. Bu dublikat request emas; ortiqcha tushum kredit bo‘lib qoladi. |
| 11 | Desktopda kreditning bir qismini boshqa qarz hisobiga o‘tkazing. | Telefonga credit_transferning ikkala qatori yetadi: manba kredit kamayadi, maqsad qarz kamayadi; mijozning umumiy net balansi o‘zgarmaydi. |
| 12 | Qolgan kreditning bir qismini desktopdan naqd qaytaring. Tasdiqlashni ikki marta bosing. | Bitta credit_refund; chiqim va musbat debt delta telefonga yetadi. |
| 13 | Telefonda mijozni offline tahrirlang/bloklang, ilovani qayta ochib ulang. | Oxirgi qiymatlar desktopga yetadi. Batafsil konflikt ssenariylari: `DEBT_STAGE2A_CUSTOMER_SYNC_UZ.md`, 1–9. |
| 14 | Test nusxasida eski desktopga yangi telefonni ulang. | Yangi turdagi amallar o‘chirilmaydi. Ikkala dastur yangilangach eski qolgan amallar ham olinadi. Eski versiyada yangi operatsiyalar bilan ishlash tavsiya etilmaydi. |

**Yakuniy branch uchun:** `fix/debt-stage3-complete` da 3-bosqich tuzatishlari ham mavjud. Ushbu testlarni `docs/DEBT_FINAL_MANUAL_TEST_UZ.md` bilan birga bajaring; musbat delta kelgach ekrandagi qoldiq ham mos bo‘lishi shart. Eski 2-bosqich branchining o‘zi bu qoldiq tuzatishlarini o‘z ichiga olmaydi.

## Qo‘shimcha texnik manual tekshiruv (lokal agent yordamida)

Quyidagilar oddiy knopka bosish bilan ishonchli tekshirilmaydi. Hozirgi mobil UI’da reverse/refund/transfer servis metodlariga chaqiriq topilmadi; yangi tugmalar qo‘shilmadi. Shuning uchun quyidagi API/servis ssenariylarini lokal agent mavjud autentifikatsiyalangan test ulanishida bajarishi mumkin; haqiqiy mijoz bazasida emas.

1. `/api/v2/debt/reverse_payment`, `/refund_credit`, `/transfer_credit`: bir xil `RequestGuid`, `OccurredAt` va body bilan ikki POST yuboring. Ikkalasida bir xil `resultGuid`; bazada bitta event bo‘lsin. Bir xil ID bilan summani o‘zgartirish rad etilsin. IDsiz so‘rov rad etilsin.
2. Telefonning `*OnDesktop` metodini offline chaqiring. Birinchi chaqiriq “navbatda saqlandi” holatini bildiradi; bu pul amali bajarildi degani emas. Ilovani qayta ochib ulang. Natija tasdiqlangach aynan shu parametrli qayta chaqiriq saqlangan resultni beradi. Natija iste’mol qilingach `acknowledgeDebtCommand(kind, subject)` chaqiriladi. Shundan keyingina xuddi shu subyektga yangi mustaqil amal boshlanadi. Noma’lum natijani acknowledge qilish rad etiladi.
3. Server commitidan keyin HTTP javobini ataylab yo‘qoting. Qayta yuborish o‘sha RequestGuid bilan borib, yangi pul harakati yaratmasin. Oddiy Wi-Fi uzish kerakli vaqtga tushishini kafolatlamaydi.
4. Lokal payment commitidan keyin UI natijani tasdiqlashidan oldin processni to‘xtating. Qayta ochib mijozning to‘lov oynasini ochganda “oldingi to‘lov saqlangan” chiqsin; yangi payment yaratilmasin.
5. Avval yangi correction paketini, keyin uning asl payment/account/return paketini yetkazing. Dastlab inboxda kutishi, keyin bir marta qo‘llanishi kerak. Bir xil paketni qayta yuborish natijani o‘zgartirmasin.
6. Eski bazada navbatga tushmagan return_offset/return_reversal bor nusxani oching. Server qayta ishga tushgach ular eksport qilinsin. Immutable tarix satrlari o‘zgarmasin.

Kerak bo‘lsa bazaning nusxasida faqat o‘qish uchun:

```sql
-- Bitta request uchun bitta event bo‘lishi kerak.
SELECT request_guid, COUNT(*) FROM debt_events
GROUP BY request_guid HAVING COUNT(*) > 1;

-- To‘g‘ri matematik balans: boshlang‘ich qarz + barcha ishorali deltalar.
SELECT a.guid, a.customer_guid,
       a.original_debt_minor + COALESCE(SUM(l.debt_delta_minor), 0) AS balance_minor
FROM debt_accounts a
LEFT JOIN debt_event_lines l ON l.account_guid = a.guid
GROUP BY a.guid, a.customer_guid, a.original_debt_minor;

SELECT guid,kind,request_guid,reference_guid,cash_minor,card_minor
FROM debt_events ORDER BY occurred_at,device_sequence;
SELECT packet_guid,error FROM debt_sync_inbox;
```

`balance_minor` tiyin hisobida; so‘mga o‘tish uchun 100 ga bo‘linadi. Test muvaffaqiyatli deb belgilashdan oldin ikkala qurilmaning tarixini va summalarini solishtiring. Xato bo‘lsa bazani tozalamang: test raqami, qurilma, ketma-ket amallar, kutilgan/amaldagi natija va xabarni yuboring.

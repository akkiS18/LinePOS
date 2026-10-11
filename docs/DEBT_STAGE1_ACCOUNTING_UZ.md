# Qarz daftari: 1-bosqich — hisobot va karta komissiyasi

Asos: main `37492e4631efde46df81365cfcbdf4aa4be6821b`.
Branch: `fix/debt-stage1-accounting`.
Scope: auditdagi 4 va 6-muammo. UI/UX maketlari, Firebase litsenziyalash,
CBU kurs olish, manfiy qoldiq siyosati o'zgartirilmagan.

## O'zgarishlar

- Bekor qilishdagi manfiy cash/card/fee deltalari hisobotda qo'shiladi.
  100 000 to'lov va -100 000 bekor qilish sof tushumi 0.
- Desktop va telefon qarz undirishda sozlangan karta foizini qo'llaydi.
  Naqd qismdan komissiya olinmaydi. Hisob tiyinlarda decimal bilan,
  yarim tiyin yuqoriga yaxlitlanadi. Qarz to'langan jami summaga kamayadi;
  komissiya qarzga qo'shilmaydi va mijoz to'lovidan ayirilmaydi.
- Komissiya uchun to'lov vaqtidagi lokal kesh USD kursi saqlanadi.
  Tarmoq/CBU so'rovi qo'shilmagan. Bir xil request ID qayta kelganda
  oldingi komissiya/kurs olinadi, qolgan maydonlarni repository tekshiradi.
- Davriy umumiy hisobot va Excel sof foydasidan qarz komissiyasi bir marta
  ayiriladi. USD xarajat har bir hodisaning saqlangan kursida hisoblanadi.
  Qaytarilgan komissiya manfiy xarajat bo'lib hisoblanadi.
- Mahsulot/kategoriya/ombor/chek turi bo'yicha toraytirilgan hisobotdagi
  savdo marjasiga butun do'konning davriy qarz komissiyasi yuklanmaydi.
  Qarz komissiyasi alohida davriy qarz statistikasi bo'lib qoladi.
- Eski nol komissiyali yozuvlar qayta yozilmaydi. Eski musbat komissiyaning
  tarixiy USD kursi bo'lmasa dollar foydasi to'liq deb ko'rsatilmaydi;
  bugungi kurs bilan tarix taxmin qilinmaydi.

## Tekshirish

Yangi testlar:
- `tests/Business.CoreTests/DebtStage1AccountingTests.cs`
- `app/src/androidTest/java/uz/pos/electro/data/business/DebtStage1AccountingTest.kt`

Ssenariy: 100 000 so'm qarz; 40 000 naqd + 60 000 karta; 1.8% komissiya;
12 000 USD kursi. Natija: 1080 so'm xarajat / $0.09, qarz 0.
Foiz va kurs o'zgarganidan keyin bir xil so'rov: ikkinchi to'lov yo'q,
eski fee/FX o'zgarmaydi. To'lovni 540 so'm komissiya qaytarib bekor qilish:
sof tushum 0, qolgan xarajat 540 so'm / $0.045, qarz yana 100 000.
Alohida bekor qilish kunida sof tushum -100 000, xarajat -540.
Sana [start,end), cash-only, yarim tiyin, katta qiymat chegarasi ham tekshiriladi.

PR: https://github.com/akkiS18/LinePOS/pull/3 (draft; main'ga merge yo'q).
Tekshirilayotgan runtime commit: `815f2acd08129cf9a8763a49ce140487f800fd7e`.

- Lokal 23 Python testi o'tdi; `git diff --check` toza.
- Windows desktop build o'tdi.
- Alohida `dotnet run --project tests/Business.CoreTests -- --stage1` o'tdi
  (CI run `38146784343`, job `114491713485`, birinchi regression step).
- Android build va yakuniy Room natijasi quyida checkpointda qayd qilinadi.

Umumiy CI yashil emas. Mavjud testlarni o'chirish/yumshatish qilinmadi:
- WifiSync.CoreTests: minimal test bazasida debt_customers yo'q, lekin main'dagi
  WifiSyncStore.Pull shu jadvalni o'qiydi. Business full suite shu sabab skipped.
- Dastlabki API 26/35 runlarda 32 testdan 6 tasi yiqildi; yangi stage-1 testi
  yiqilmadi. Eski testlar: DebtLanSyncTest tokenni `token` kalitiga yozadi,
  manager `wifi_v2_token`ni o'qiydi; DebtMigrationTest hash fixture cheklovdan
  o'tmaydi; DebtMobileIntegrationTest sale GUID kutadi, repository request GUID
  qaytaradi; uchta DebtMultiDeviceConvergenceTest oddiy Room builder ishlatib,
  auxiliary debt sxemasini o'rnatmaydi. Bu fayllar ushbu branchda o'zgarmagan.
- Bu to'siqlar yakuniy regressiya bosqichida to'g'ri fixturelar bilan tekshirilishi
  kerak; ularning o'rniga 'barcha testlar success' deb xulosa qilinmasin.

Lokal .NET CLI process metadata cheklovi sabab ishlamadi; .NET dalili GitHub
runnerdan olingan. Haqiqiy Windows+telefon LAN manual testi bajarilmadi.

## Keyingi scope

2-bosqich: 2, 5, 7 — barcha moliyaviy amal paketlari, mijoz tahriri navbati,
HTTP takror so'rov himoyasi.
3-bosqich: 1, 3 — qoldiq snapshot/debt delta kelishuvi, birinchi ulanish,
store identifikatori, restore va to'liq qurilmalararo regressiya.
Bu branch qolgan muammolarni tuzatgan yoki butun loyiha productionga tayyor
bo'lgan deb hisoblanmasin.

## Qo'lda qabul testi (test bazasida)

1. Karta komissiyasini 1.8%, USD kursini 12 000 qilib, 100 000 so'mlik
   qarz oching. 40 000 naqd va 60 000 karta bilan undiring.
2. Qarz 0, undirilgan summa 100 000, komissiya 1080 bo'lishi kerak.
   Faqat to'lov bo'lgan kunda savdo tushumi 0; umumiy sof foyda -1080 so'm,
   -$0.09. Excel ham shu umumiy summalarni berishi kerak.
3. Kursni 24 000 va komissiyani 3% qilib, oldingi davrni qayta oching:
   eski to'lov komissiyasi va dollar xarajati o'zgarmasligi kerak.
4. Desktopda to'lovni bekor qiling. UI mavjud xulqiga ko'ra to'liq
   komissiyani ham bekor qiladi: ikkala amal joylashgan davrda sof
   undirish 0 va sof komissiya 0; qarz yana 100 000.
5. Faqat bekor qilish kunini tanlang: sof undirish manfiy ko'rinishi kerak.
   Naqd-only to'lov uchun komissiya 0 bo'lishi kerak.

Bu bosqichda qaytarish/bekor qilishni ikkinchi qurilmaga sinxronlash hali
qabul mezoni emas: transportdagi audit #2 keyingi bosqichda tuzatiladi.

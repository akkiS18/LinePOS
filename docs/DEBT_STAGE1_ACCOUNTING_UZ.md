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

Lokal 23 Python testi o'tdi. Lokal .NET CLI muhitning process ma'lumotini
ololmagani sabab ishga tushmadi; desktop va Android test/build dalili PR CI'da
tekshirilishi kerak. Bu fayldagi holat keyingi checkpointda yangilanadi.

## Keyingi scope

2-bosqich: 2, 5, 7 — barcha moliyaviy amal paketlari, mijoz tahriri navbati,
HTTP takror so'rov himoyasi.
3-bosqich: 1, 3 — qoldiq snapshot/debt delta kelishuvi, birinchi ulanish,
store identifikatori, restore va to'liq qurilmalararo regressiya.
Bu branch qolgan muammolarni tuzatgan yoki butun loyiha productionga tayyor
bo'lgan deb hisoblanmasin.

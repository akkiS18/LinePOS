# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-05. Holat: **1-bosqich yakunlandi — reja, kod emas**.

## Asos va branch

- Repo: akkiS18/LinePOS.
- Asos: main `b631a27241a5eb25501a6e4ab316c4aebdae4460`, tree `1f4322888d922eba93338bba224b7963c07e4b24`.
- Branch: `feature/customer-debt`.
- To‘liq kontrakt: [DEBT_PLAN_UZ.md](DEBT_PLAN_UZ.md).
- Bu checkpoint joylashgan commit — 1-bosqich checkpointi; o‘z commit SHA sini fayl ichiga taxminan yozmang, git logdan oling.

## Bajarildi

- Main’dan savdo, Room13, desktop SQLite, sync push/pull/freeze, returns, accounting, backup va navigatsiya kodlari o‘qildi.
- Faqat UZS xaridor qarzi; desktopdan uzilgan telefonda ham yakuniy nasiya/payment talabi qayd etildi.
- Immutable eventlar, ortiqcha to‘lov, atomic group, precision, returns va report/fee qoidalari belgilandi.
- Frontend joylashuvi va 27 ta qabul testi yozildi.
- Ish 2A/2B, 3A/3B, 4, 5, 6A/6B, 7 kichik yakunlarga bo‘lindi.
- Runtime, migratsiya va ilova UI kodi o‘zgartirilmadi. Main’ga merge yo‘q. Build/runtime testlar ishlatilmadi: bu bosqich faqat hujjat.

## Keyingi sessiya — faqat 2A

1. Remote branch va main holatini tekshir; foydalanuvchining dirty worktree fayllarini qo‘shma/o‘chirma. Toza alohida checkout ol.
2. AGENT.md va DEBT_PLAN_UZ.md ni o‘qi. Yangi main o‘zgarishlari bo‘lsa moslashtirishni kichik alohida commitda qil.
3. Platformadan mustaqil money/ledger modeli: minor units, event lines, payment allocation, signed balance va credit projection.
4. Umumiy fixtures: D01, D03–D05, D13 arifmetikasi, D16 transfer arifmetikasi, D17 fee ajratilishi; C# va Kotlin natijalari teng.
5. Faqat hisoblash qatlamini tugat; migratsiya/sync/UIga kirishma. Tegishli testni bajar, aniq natija va cheklovni yoz.
6. Shu branchga commit/push, checkpointni yangila, to‘xta. Limitni aniq bilaman deb vaqt va’da qilma.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- Reja testlari o‘tgan deb yozma: 2–7 bosqichlar bajarilmagan.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

## Lokal nusxa haqida

Tekshiruv paytida `LinePOS-business-fixes` checkouti dirty edi va lokal HEAD remote main emas edi. Unga tegilmadi. Hujjatlar alohida `LinePOS-debt-plan/docs` ichida tayyorlandi; GitHub commit bevosita tekshirilgan main tree asosida faqat shu ikki hujjatni qo‘shadi. Eski lokal nusxani yangi branch asosi deb qabul qilmang.

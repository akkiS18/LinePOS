# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-05. Holat: **2A-bosqich yakunlandi — hisoblash yadrosi; integratsiya hali yo‘q**.

## Asos va branch

- Repo: akkiS18/LinePOS.
- Asos: main `b631a27241a5eb25501a6e4ab316c4aebdae4460`, tree `1f4322888d922eba93338bba224b7963c07e4b24`.
- Branch: `feature/customer-debt`.
- To‘liq kontrakt: [DEBT_PLAN_UZ.md](DEBT_PLAN_UZ.md).
- Bu checkpoint joylashgan commit — eng yangi bosqich checkpointi; o‘z commit SHA sini fayl ichiga taxminan yozmang, git logdan oling.

## Bajarildi

- Main’dan savdo, Room13, desktop SQLite, sync push/pull/freeze, returns, accounting, backup va navigatsiya kodlari o‘qildi.
- Faqat UZS xaridor qarzi; desktopdan uzilgan telefonda ham yakuniy nasiya/payment talabi qayd etildi.
- Immutable eventlar, ortiqcha to‘lov, atomic group, precision, returns va report/fee qoidalari belgilandi.
- Frontend joylashuvi va 27 ta qabul testi yozildi.
- Ish 2A/2B, 3A/3B, 4, 5, 6A/6B, 7 kichik yakunlarga bo‘lindi.
- Runtime, migratsiya va ilova UI kodi o‘zgartirilmadi. Main’ga merge yo‘q. Build/runtime testlar ishlatilmadi: bu bosqich faqat hujjat.

## 2A natijasi

- `desktop/PosElectro.Desktop/Debt/DebtAccounting.cs` va mobil `data/debt/DebtAccounting.kt`: minor-unit parsing, nasiya qoldig‘i, payment allocation, balance/credit projection, fee-aware payment reversal, return split, credit transfer/refund arifmetikasi.
- Bir xil JSON fixturelar: C# 97/97, Kotlin 97/97; barcha natijalar teng. `tests/debt/README.md` qayta ishlatish buyruqlari va API chegaralarini tushuntiradi.
- Yangi `Debt accounting core` workflow normal .NET loyiha buildi va Kotlin paritetini tekshiradi. GitHub natijasi sessiya yakunida qayd etiladi.
- Lokal .NET CLI Process.GetStat xatosi sabab bevosita SDK Roslyn kompilyatori ishlatildi; net8.0 reference assemblies, warnings-as-errors va nullable yoqilgan. Natijadagi assembly .NET runtime bilan haqiqatan ishga tushdi.
- Toza checkout: `LinePOS-debt-core`. Eski dirty nusxaga tegilmadi.
- Baza, UI, sync va mavjud sale/report yo‘llariga ulanmagan. Bu bosqichdagi split/refund/transfer — faqat hisoblash; pul qaytarishni ishga tushirmaydi.

## Keyingi sessiya — faqat 2B

1. `feature/customer-debt` holatini tekshir; AGENT.md, DEBT_PLAN_UZ.md va tests/debt/README.md ni o‘qi. Dirty fayllarni tasodifan commit qilma.
2. Room versiyasini qayta tekshir (asosda 13); additive debt jadvallari, indekslar va FKlar. Desktop/mobil/shared schema parity.
3. Transactional repository: customer/account/event/lines va outbox birga, immutable request GUID/hash, duplikat natijani qaytarish; core yordamida hisoblash.
4. GUID/customer/store ownership validatsiyasi repositoryda; 2A sof hisoblashni authorization yoki deduplication deb qabul qilma.
5. Fresh DB, upgrade/backup, FK, rollback, bir request replay/changed-body testlarini bajar. Balancega eventni ikki marta bermaslikni tekshir.
6. Sync transport yoki UIga kirishma. Shu branchga commit/push, checkpointni yangila, to‘xta. Main’ga merge qilma.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- To‘liq D01–D27 reja testlari o‘tgan deb yozma: faqat 2A arifmetika fixturelari o‘tdi; 2B–7 bajarilmagan.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

## Lokal nusxa haqida

Tekshiruv paytida `LinePOS-business-fixes` checkouti dirty edi va lokal HEAD remote main emas edi. Unga tegilmadi. Hujjatlar alohida `LinePOS-debt-plan/docs` ichida tayyorlandi; GitHub commit bevosita tekshirilgan main tree asosida faqat shu ikki hujjatni qo‘shadi. Eski lokal nusxani yangi branch asosi deb qabul qilmang.

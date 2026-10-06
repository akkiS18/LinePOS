# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-05. Holat: **2B-1 yakunlandi — sxema va xavfsiz migratsiya; repository hali yo‘q**.

## Asos va branch

- Repo: akkiS18/LinePOS.
- Asos: main `b631a27241a5eb25501a6e4ab316c4aebdae4460`, tree `1f4322888d922eba93338bba224b7963c07e4b24`.
- Branch: `feature/customer-debt`.
- To‘liq kontrakt: [DEBT_PLAN_UZ.md](DEBT_PLAN_UZ.md).
- Bu checkpoint joylashgan commit — eng yangi bosqich checkpointi; o‘z commit SHA sini fayl ichiga taxminan yozmang, git logdan oling.

## 1-bosqichda bajarildi

- Main’dan savdo, Room13, desktop SQLite, sync push/pull/freeze, returns, accounting, backup va navigatsiya kodlari o‘qildi.
- Faqat UZS xaridor qarzi; desktopdan uzilgan telefonda ham yakuniy nasiya/payment talabi qayd etildi.
- Immutable eventlar, ortiqcha to‘lov, atomic group, precision, returns va report/fee qoidalari belgilandi.
- Frontend joylashuvi va 27 ta qabul testi yozildi.
- Ish 2A/2B, 3A/3B, 4, 5, 6A/6B, 7 kichik yakunlarga bo‘lindi.
- Runtime, migratsiya va ilova UI kodi o‘zgartirilmadi. Main’ga merge yo‘q. Build/runtime testlar ishlatilmadi: bu bosqich faqat hujjat.

## 2A natijasi

- `desktop/PosElectro.Desktop/Debt/DebtAccounting.cs` va mobil `data/debt/DebtAccounting.kt`: minor-unit parsing, nasiya qoldig‘i, payment allocation, balance/credit projection, fee-aware payment reversal, return split, credit transfer/refund arifmetikasi.
- Bir xil JSON fixturelar: C# 97/97, Kotlin 97/97; barcha natijalar teng. `tests/debt/README.md` qayta ishlatish buyruqlari va API chegaralarini tushuntiradi.
- Yangi `Debt accounting core` workflow normal .NET loyiha buildi va Kotlin paritetini tekshiradi. GitHub run [37285212424](https://github.com/akkiS18/LinePOS/actions/runs/37285212424) SUCCESS: normal .NET loyiha buildi, C# 97/97, Kotlin 97/97 va paritet. Tekshirilgan kod commit: `62150310d30a6fe27020194758eba86b76eb3fa6`.
- Lokal .NET CLI Process.GetStat xatosi sabab bevosita SDK Roslyn kompilyatori ishlatildi; net8.0 reference assemblies, warnings-as-errors va nullable yoqilgan. Natijadagi assembly .NET runtime bilan haqiqatan ishga tushdi.
- Toza checkout: `LinePOS-debt-core`. Eski dirty nusxaga tegilmadi.
- Baza, UI, sync va mavjud sale/report yo‘llariga ulanmagan. Bu bosqichdagi split/refund/transfer — faqat hisoblash; pul qaytarishni ishga tushirmaydi.

## 2B-1: sxema va migratsiya

Foydalanuvchining kichik tugallangan bosqichlarda ishlash talabi sabab 2B ikkiga bo‘lindi: 2B-1 sxema/migratsiya, 2B-2 transactional repository. Bu commit 2Bni to‘liq yakunlamaydi.

- `debt/schema.sql`, desktop resource va Android asset aynan bir xil. Auxiliary SQLite tables: schema/scope/customers/events/accounts/event_lines/command_receipts/sync_inbox.
- Room 13→14 migration oldindan `before-debt` snapshot oladi. Fresh/open callback bir xil sxemani o‘rnatadi. Room entitylari o‘zgarmadi; debt sxemasi installer tomonidan sqlite_master definitsiyasi va FK orqali tekshiriladi.
- Desktop old bazani yangi debt sxemasidan oldin snapshot qiladi; qayta ochishda takror backup yo‘q. Debt schema o‘zining version=1 markeriga ega; Android user_version=14, desktopning mavjud user_version qiymatini majburan o‘zgartirmaydi.
- Installer odatiy ochilishda writer tranzaksiyasidagi FK nazoratini talab qiladi; Room migratsiya davrida DDL/FK integrity check bajarilib, onOpen’da qat’iy tekshiriladi. Schema o‘rnatish atomik. Noma’lum versiya, partial/mismatched schema yoki broken FK bo‘lsa jim tuzatish yo‘q.
- Composite FK customer/store tegishliligini saqlaydi; request/device-sequence unique; signed integer pul, o‘zgarmas financial history va bir targetga yagona reversal cheklovlari.
- Lokal Python SQLite: 14 yangi debt schema + 7 mavjud sync test = 21/21 PASS. Bu repository testlari emas.
- Tekshirilgan kod commit: `a0b7060b917825510faec18068dd1d373146f689`.
- [CI 37332080525](https://github.com/akkiS18/LinePOS/actions/runs/37332080525): core (Wi-Fi/business/migration/Python), Windows desktop build, Android build, API26 va API35 Room instrumentatsiya — hammasi SUCCESS.
- [Debt core CI 37332080765](https://github.com/akkiS18/LinePOS/actions/runs/37332080765): C# 97/97, Kotlin 97/97 va natijalar pariteti SUCCESS.
- Tekshiruvda Room migration→onOpen FK lifecycle va API35 WAL reader/writer PRAGMA farqi hisobga olindi. Oldingi `035f7dc` runida API35’dagi tranzaksiyadan tashqari FK assertion yiqilgan edi; writer tranzaksiyasi va real orphan INSERT rad etilishi bilan test kuchaytirildi, yakuniy run o‘tdi.
- Fizik Windows/telefon/printer/Telegram sinovi bajarilmadi. Bu bosqichda yangi qarz UI yoki release binari yo‘q. Sxema va keyingi qatlam chegaralari: `docs/DEBT_SCHEMA.md`.
- UI, repository, qarz journal capture/push/pull hali yo‘q. Sxema hech qanday eski DEBT chekdan avtomatik qarz yaratmaydi.

## Keyingi sessiya — faqat 2B-2

1. Remote branch/checkpoint va CI natijalarini tekshir; mavjud 2A va 2B-1 testlarini saqla.
2. Transactional repository: customer/account/event/lines va mavjud sync_journal outbox birga; immutable request GUID/canonical hash, replay qaytargan eski natija.
3. Opening account `original_debt_minor` boshlang‘ich qoldiq; `sale_open` header audit/dependency uchun, opening summani yana debt_event_linesga yozib ikki marta hisoblama.
4. Sxema CHECK/FK yetarli emas: request/effect summalari, event kind/reference, GUID format, store/actor identity, freeze/seal va authorization repositoryda tekshirilishi kerak. To‘liq eventdan keyin qo‘shimcha line yozishni repository bloklasin.
5. Due date va tarixiy customer_name accountda immutable. Keyingi muddat tahriri kerak bo‘lsa alohida audit modeli; original accountni update qilishga shoshilma.
6. Store scope avtomatik seed qilinmagan. Offline yangi do‘kon identifikatori, device instance/sequence va restore identity siyosatini reja bo‘yicha repository bilan yarat.
7. Aynan bir ulanish/tranzaksiyada sale/account/event/outbox va rollback, identical retry/changed body, noto‘g‘ri customer/store, double submission testlari. Yangi payment hali transportga ketmasin: stage3 capability gate talab etiladi.
8. Sync/UIga kirishma. Test/commit/push va checkpoint; main’ga merge qilma.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- To‘liq D01–D27 reja testlari o‘tgan deb yozma: 2A arifmetika va 2B-1 migratsiya/sxema testlari o‘tdi; 2B-2 va 3–7 bajarilmagan.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

## Lokal nusxa haqida

Tekshiruv paytida `LinePOS-business-fixes` checkouti dirty edi va lokal HEAD remote main emas edi. Unga tegilmadi. Hujjatlar alohida `LinePOS-debt-plan/docs` ichida tayyorlandi; GitHub commit bevosita tekshirilgan main tree asosida faqat shu ikki hujjatni qo‘shadi. Eski lokal nusxani yangi branch asosi deb qabul qilmang.

# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-07. Holat: **2B-2 yakunlandi — lokal transactional repository; sync/UI integratsiyasi hali yo‘q**.

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
- 2B-1 yakunida UI, repository, qarz journal capture/push/pull yo‘q edi. Sxema hech qanday eski DEBT chekdan avtomatik qarz yaratmaydi.

## 2B-2: transactional repository

- C# va Kotlin `DebtRepository`: do‘konga binding, mijoz yaratish, atomik nasiya ochish, qarz to‘lovi va qoldiq proyeksiyasi. Pul integer minor-unit, tarixiy ism/muddat o‘zgarmas.
- Bir xil request/body eski natijani qaytaradi; o‘zgargan body rad etiladi. Sale callback replayda chaqirilmaydi; payment qayta taqsimlanmaydi. Request actor/storega bog‘langan.
- Customer yaratishning asl snapshoti `sync_meta`da saqlanadi; keyin ism/archive o‘zgarsa ham eski yaratish retryi taniladi. GUID/payload, aktiv mijoz, do‘kon va host permission tekshiriladi.
- Har repository instance uchun yangi writer epoch UUID; restart/DB nusxasida sequence to‘qnashmaydi. Fizik qurilma yorlig‘i va server restore cursor siyosati hali 3Bda.
- Sale/account/event/receipt/outbox bir tranzaksiya; payment header/frozen lines/receipt/outbox bir tranzaksiya. Callback tashlagan xato va journal INSERTdagi xato to‘liq rollback qiladi.
- Mahalliy command remote `applying` yoki ochiq `current_group` ichida ishlamaydi. Arbitrary line append API yo‘q.
- Outbox `acked=-1` bilan HELD. Nasiya guruhidagi sale/stock journal ham ushlab turiladi. **Desktop full pull sale jadvalini journal holatidan mustaqil o‘qishi mumkin; bu yakka holda barcha eski transportga qarshi himoya emas. Stage3 tugamasdan UI/cashierga ulash mumkin emas.**
- Hozir UI/cashier/sync call site yo‘q. Callback savdo/items/stockni berilgan tranzaksiyada saqlashi va basket fingerprintni to‘g‘ri tuzishi shart. Host authorization/store setup adapterlari keyingi integratsiyada yoziladi.
- API, test va transport chegaralari: [DEBT_REPOSITORY.md](DEBT_REPOSITORY.md).
- Tekshirilgan kod commit: `3b2a77ed1276289b24a400370ca30bb86af199bf` (asosiy repository commit `529d18a125a2071576308018cadc4bfa51b32305`).
- [CI 37607020561](https://github.com/akkiS18/LinePOS/actions/runs/37607020561): haqiqiy SQLite repository/regressiya, 21 Python testi, Windows desktop build, Android build hamda API26/API35 Room instrumentatsiya — hammasi SUCCESS. Har Android emulyatorida 13 ta test, shu jumladan yangi repository ssenariysi ishladi.
- [Debt core CI 37607020731](https://github.com/akkiS18/LinePOS/actions/runs/37607020731): C# 97/97, Kotlin 97/97 va paritet SUCCESS. Lokal Python 21/21 va diff whitespace tekshiruvi ham o‘tdi.
- Real telefon/Windows UI/printer/Telegram sinovi bajarilmadi; bu bosqichda UI yoki yangi release binari yo‘q. Main’ga merge qilinmadi.

## Keyingi sessiya — faqat 3A, avval hajmni ajratish

1. Remote branch/checkpoint va eng yangi CI holatini tekshir. Mavjud schema/core/repository testlarini saqla; main’ga merge yo‘q.
2. `debtLedgerV1` capability va ishonchli store identity/pairing kontraktini belgilab, har ikki yo‘nalishda eski peerga moliyaviy guruh ketmasligini ta’minla. Full pull/snapshot/legacy endpointlar ham audit qilinsin; `acked=-1`ning o‘zi yetmaydi.
3. Wire packetda customer/account/sale/header va frozen event lines dependency tartibida bo‘lsin. Canonical local command snapshoti to‘liq wire event emas. Qabul qiluvchi mahalliy `TakePayment`ni chaqirib qayta allocation qilmasin.
4. Receiver validation/ownership/idempotency va missing-dependency inboxni atomik yozsin. Header/line komplekti to‘liq bo‘lmasa ACK/cursor oldinga yurmasin; bir guruhning faqat sale/stock qismi chiqarilmasin.
5. HELD journalni faqat tayyor to‘liq packetga aylantirgach release qil; restart/ACK yo‘qolishi/reorder/duplicate va incompatible peer testlarini yoz.
6. 3A bir sessiyaga katta bo‘lsa 3A-1 packet/validator, 3A-2 transport/ACK kabi tugallangan qismlarga bo‘l; release UI yoqilmasin. Har yakunda test/commit/push/checkpoint.
7. 3B convergence/restore/contact conflict, 4/5 frontend, 6 returns/report, 7 backup/manual hali alohida. Qarz undirish revenue emas; offline ortiqcha undirish eventi yo‘qolmasin.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- To‘liq D01–D27 reja testlari o‘tgan deb yozma: 2A arifmetika, 2B-1 migratsiya/sxema va 2B-2 repository alohida testlanadi; 3–7 bajarilmagan.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

## Lokal nusxa haqida

Tekshiruv paytida `LinePOS-business-fixes` checkouti dirty edi va lokal HEAD remote main emas edi. Unga tegilmadi. Hujjatlar alohida `LinePOS-debt-plan/docs` ichida tayyorlandi; GitHub commit bevosita tekshirilgan main tree asosida faqat shu ikki hujjatni qo‘shadi. Eski lokal nusxani yangi branch asosi deb qabul qilmang.

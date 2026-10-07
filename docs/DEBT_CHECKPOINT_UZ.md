# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-07. Holat: **3A-1 yakunlandi — canonical wire component / validator; haqiqiy qarz transporti va UI hali ulanmagan**.

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

## 3A-1: wire component / validator

- `DebtWire.cs` / `DebtWire.kt`: customer-create, sale_open va payment komponentlari uchun canonical codec. UTF-8/base64/string-number format float orqali pul yo‘qotmaydi; butun event/account/line tartibi uchun SHA-256 fingerprint.
- Qat’iy GUID, do‘kon/actor/request/payload mosligi, original debt, due date, summa/fee va line count/uniqueness/taqsimot tekshiruvi. Receiver qoldig‘iga qarab paymentni qayta taqsimlamaydi. Decoded line list o‘zgarmas.
- Pure `RequirePeer`: do‘kon GUID va `debtLedgerV1` capability tekshiruvi. Bu hali pairing handshake emas; capability ilovada e’lon qilinmagan va HELD navbat ochilmagan.
- 92 ta umumiy wire fixture: C#/Kotlin va Android uchun bir xil; real repository payloadlariga bog‘langan. 2^53 dan katta pul, Int64 limit, buzilgan UTF-8/base64, boshqa do‘kon, eski peer, noma’lum schema/kind, noto‘g‘ri summa/fee/date, duplicate/missing line kabi holatlar.
- To‘liq atomic sale/stock envelope yoki DB receiver hali yo‘q. Hash autentifikatsiya o‘rnini bosmaydi. DBdagi account/customer ownership va request/full-content replay receiverda tekshirilishi shart.
- Audit: desktop full pull HELDdan mustaqil sales o‘qiydi. `download_db` sync_meta’ni ham o‘chiradi, unda customer-create retry snapshot bor. Metadata coalescing/500-limit ham moliyaviy guruhlar uchun qayta ko‘rilishi shart. Batafsil: [DEBT_WIRE.md](DEBT_WIRE.md).
- Implementatsiya commit: `fc32b4f741e7d3b8f79fea85ca84542ad35c60b6`. [CI 37644570634](https://github.com/akkiS18/LinePOS/actions/runs/37644570634) SUCCESS: SQLite/core/regressiya, 22 Python testi, Windows desktop build, Android build va API26/API35 instrumentatsiya. Har emulyatorda 14 test, shu jumladan 92 wire fixtureli test ishladi.
- Paritet workflow argumenti tuzatilgan commit: `59368038342ec5106bc3e2bb2fdd97f9493c7a05` (faqat workflow buyrug‘i; app kodi o‘zgarmadi). [CI 37645024740](https://github.com/akkiS18/LinePOS/actions/runs/37645024740) SUCCESS: C# 92/92 + 97/97, Kotlin 92/92 + 97/97, ikkala korpus uchun natijalar teng.
- Birinchi paritet run `37644570804`da barcha C#/Kotlin misollari o‘tgan, lekin yakuniy Python comparatorga ortiqcha argument berilgani sabab workflow yiqilgan. Keyingi run aynan shu buyruq tuzatilgach muvaffaqiyatli tugadi.
- Lokal Python 22/22 va `git diff --check` PASS. Fizik qurilma/LAN/ACK testlari bu bosqichda bajarilmagan. Main’ga merge yoki release yo‘q.

## Keyingi sessiya — 3A-2, avval yana kichik scope

1. Remote branch/checkpoint va CI dalilini tekshir. `docs/DEBT_WIRE.md`dagi API/chegaralar va transport auditini o‘qi; main’ga merge yo‘q.
2. 3A-2ni kerak bo‘lsa **3A-2a: DB export/freeze + atomic receiver**, **3A-2b: handshake/push/pull/ACK**ga bo‘l. Bir sessiyada barcha integrationni majburan tugatishga urinma.
3. Do‘kon GUIDni trusted setup/pairingdan ol. Stable store va server restore epoch alohida; peerning o‘zi yuborgan store’ni trusted expectedStore qilib ishlatma. `debtLedgerV1` barcha yo‘llar himoyalanmaguncha e’lon qilinmasin.
4. DBdan asl customer-create snapshot/header/account/frozen linesni bir snapshotda chiqar. Eski outbox payload command array, yangi full envelope emas. Butun canonical event hashni durable dedupda saqla; request hashning o‘zi taqsimot tamperini tekshirmaydi.
5. Customer/sale/items/account/event/stock to‘liq financial envelope bo‘lsin. Receiver dependency/ownership/permissionni tekshirib, local `TakePayment`ni chaqirmasdan frozen delta’larni atomik yozsin. Bir xil request va boshqa body ko‘rinadigan integrity xatosi; valid kechikkan payment yo‘qolmasin.
6. Missing dependency yoki unknown kind/schema inbox/errorga; commit bo‘lmasdan ACK/cursor yo‘q. Reorder/duplicate/ACK yo‘qolishi/rollbackni haqiqiy SQLite va Roomda tekshir.
7. Legacy full/delta pull va stripped `download_db`ni ham debt-aware qil. `acked=-1` yakka o‘zi himoya emas; metadata coalescing HELD dependencylarini yutib yubormasin. Codec 2MiB/10,000 line va HTTP 8MiB chegaralarini butun envelope uchun local commitdan oldin preflight yoki atomic fragmentation bilan hal qil.
8. Bir bosqich tugagach test/commit/push/checkpoint. 3B convergence/restore/contact conflict, 4/5 frontend, 6 returns/report, 7 backup/manual hali alohida.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- To‘liq D01–D27 reja testlari o‘tgan deb yozma: 2A arifmetika, 2B-1 migratsiya/sxema, 2B-2 repository va 3A-1 codec testlari o‘tdi; 3A-2 hamda 3B–7 bajarilmagan.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

## Lokal nusxa haqida

Tekshiruv paytida `LinePOS-business-fixes` checkouti dirty edi va lokal HEAD remote main emas edi. Unga tegilmadi. Hujjatlar alohida `LinePOS-debt-plan/docs` ichida tayyorlandi; GitHub commit bevosita tekshirilgan main tree asosida faqat shu ikki hujjatni qo‘shadi. Eski lokal nusxani yangi branch asosi deb qabul qilmang.

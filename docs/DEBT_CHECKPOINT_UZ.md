# Qarz daftari — davom ettirish nuqtasi

Sana: 2026-10-10. Holat: **Qism 7 (Hisobotlar, foyda, Excel va chek/ko‘chirma) to‘liq yakunlandi va testdan o‘tdi. Navbatdagi bosqich: Qism 8 (Backup/restore, regressiya va yakuniy sinov)**.

## Asos va branch

- Repo: akkiS18/LinePOS.
- Asos: main `b631a27241a5eb25501a6e4ab316c4aebdae4460`, tree `1f4322888d922eba93338bba224b7963c07e4b24`.
- Branch: `feature/customer-debt`.
- To‘liq kontrakt: [DEBT_PLAN_UZ.md](DEBT_PLAN_UZ.md).
- Boshqa agentga topshirish: [DEBT_AGENT_HANDOFF_UZ.md](DEBT_AGENT_HANDOFF_UZ.md) — qolgan 8 qism, fayllar, testlar va tayyor prompt.
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

## 3A-2a: DB component export / atomic receiver

- `DebtSyncStore.cs` / `DebtSyncStore.kt`: production SQLite/Roomdan customer va eventni bir writer snapshotida chiqarish; incoming component batchni bitta tranzaksiyada qabul qilish.
- `sync_meta`da `debt_wire_v1:customer:<guid>` / `debt_wire_v1:event:<guid>` ostida to‘liq canonical body va SHA-256 saqlanadi. Oldingi seal ustiga yozilmaydi; o‘zgarsa integrity xatosi. Yangi schema/migratsiya yo‘q.
- Import customer → opening → payment tartibida, lekin paymentning ichki taqsimotini o‘zgartirmaydi. Account egasi/store bazadan tekshiriladi; archive bo‘lib qolgan mijozning oldin qabul qilingan offline to‘lovi yo‘qolmaydi.
- Replay DBdan tiklangan to‘liq body bilan solishtiriladi, faqat command hash emas. Mahalliy event echo’si, restart va parallel qayta yuborish yangi savdo/to‘lov/journal yaratmaydi. Sender device/sequence saqlanadi, collision rad etiladi.
- Yangi opening uchun trusted sale adapter berilgan connection/transactionda sale/items/stock yozishi shart. Total/cash/card/time tekshiriladi. Callback va seal INSERTdagi xato butun batchni rollback qiladi. Eski unrelated receiptga opening biriktirilmaydi.
- Yetishmagan dependency `DebtDependencyException` qaytaradi; **durable inbox va ACK hali yo‘q**. Host sync permission va source actor policy tekshiriladi; bu pairing/authentication implementatsiyasi emas.
- `applying=1` ordinary echo capture’ni vaqtincha o‘chiradi; qabul qilingan debt component journal HELD bo‘lib qoladi. To‘liq sale/stock relay envelope hali saqlanmaydi. Qarz component seali outer sale/stock body uchun dedup o‘rnini bosmaydi.
- API, chegaralar va testlar: [DEBT_DB_BRIDGE.md](DEBT_DB_BRIDGE.md). UI/endpoint/capability ulanmagan, main/release yo‘q.
- Birinchi CI `37683813561`da desktop/core o‘tdi; Android rollback testi `sync_journal=0` deb noto‘g‘ri taxmin qilgani uchun yiqildi. Production `WifiSyncSchema.install` standart warehouse uchun boshlang‘ich journal yozuvini yaratadi. `6caf66f` testni boshlang‘ich journal sonini saqlash va alohida debt metadata 0 bo‘lishini tekshirishga tuzatdi; production kod o‘zgarmadi.
- Yakuniy kod/test commit: `6caf66fe4453d4b802bc7d9b710959d5d53b6f24`. [Yakuniy CI 37685714116](https://github.com/akkiS18/LinePOS/actions/runs/37685714116): core, desktop build, Android build va Android API 26/35 Room testlari muvaffaqiyatli; har bir emulyatorda 15 ta test.
- [Parity CI 37683813588](https://github.com/akkiS18/LinePOS/actions/runs/37683813588), production commit `3f8f1e52de8eb6ed13dec0963b52ea77ad14a011`: C#/Kotlin hisoblash uchun 97 va wire uchun 92 ta kutilgan natija mos. Keyingi `6caf66f` faqat Android test assertionini o‘zgartirgan.
- Lokal Python testlari 22/22, `git diff --check` muvaffaqiyatli. Haqiqiy qurilma/LAN sinovi bajarilmadi; transport/UI ulanmagan. Main merge va release qilinmagan.

## 3A-2b-1: frozen financial envelope codec / validator

- 3A-2b limitga mos ikki qismga bo‘lindi: **2b-1 pure full-envelope codec/validator**, **2b-2 local freeze/preflight + atomic DB receiver/durable inbox**. Hozir faqat 2b-1.
- C#/Kotlin `DebtEnvelope`: customer-only, payment va sale_open paketlari. Nasiya paketida to‘liq tarixiy sale/items/stock delta bor; componentdagi sale fingerprint butun canonical sale bodyga bog‘langan.
- Tovar miqdori/narxi/tannarxi/kurs/komissiya aniq decimal matn, hisoblash arbitrary-precision integer bilan. Yig‘indidan keyin bir marta tiyingacha half-away rounding; Double yo‘q. UZS/USD tannarx va boshlang‘ich karta komissiyasi qayta tekshiriladi.
- Faqat DEBT sale, cash+card<total; GUID/customer/store/time/money/hash mosligi; har item delta=-quantity; item va stock op ID dublikatlari rad etiladi. Tarixiy matnlar va item tartibi fingerprintga kiradi.
- Sale 1000 item / 2MiB, envelope 6MiB. Bu limitlar local commitga hali ulanmagan; oldingi repositoryga arbitrary fingerprint bilan yozilgan test eventdan to‘liq envelope yasash mumkin emas. Local adapter before-commit preflight, haqiqiy sale numeric roundtrip va full-body durable seal keyingi qismda.
- 155 yangi umumiy fixture; C#/Kotlin/Android bir korpus va kutilgan hash/xatolarni ishlatadi. Eski 97+92 fixture saqlangan. Lokal Python 23/23, whitespace tekshiruvi PASS.
- Birinchi full CI `37731687489`: core/desktop SUCCESS, Android test kompilyatsiyasi runnerdagi JVM `JSONObject.similar()` Androidda mavjud emasligi sabab yiqildi. `2c361fb383ffa24d957f87e6c95694737eac75f0` test natijalarini mavjud Android JSON API bilan solishtirishga tuzatdi va lokal test buyruqlari hujjatini yangiladi; production kod o‘zgarmadi.
- Yakuniy kod ko‘rigida parser split max-fields+1 bilan chegaralandi: 6MiB malformed delimiter flood maydonlar ro‘yxatini cheksiz kattalashtirmaydi. 155-fixture aynan bu chegaradagi xato paketni tekshiradi. Yakuniy production/test commit: `b19c91e8be24c85b0bc5168888d1ac64e5ab983c`.
- [Yakuniy parity CI 37733139917](https://github.com/akkiS18/LinePOS/actions/runs/37733139917) SUCCESS: C#/Kotlin 97 hisob + 92 component + 155 envelope expected natijalari teng. [Yakuniy full CI 37733139869](https://github.com/akkiS18/LinePOS/actions/runs/37733139869) SUCCESS: core/regressiya, desktop build, Android build, API26 va API35 instrumentatsiya. Har emulyatorda 16 ta test, shu jumladan 155 envelope fixtureli test o‘tdi. Fizik qurilma/LAN/UI sinovi bu bosqichda bajarilmagan.
- DB schema/inbox/receiverga, network/UIga ulanmagan. HELD navbat ochilmagan; main merge/release yo‘q. Batafsil: [DEBT_ENVELOPE.md](DEBT_ENVELOPE.md).

## 3A-2b-2a: durable inbox / atomic customer-payment receiver

- `DebtEnvelopeInbox.cs/kt`: strict v1 paketni saqlash, missing dependency kutish, customer/paymentni ledger + full-body receipt + inbox completion bilan bitta writer tranzaksiyada qabul qilish.
- `DebtSyncStore` transaction/participant helperlari internal qilindi; public arbitrary sale callback yangi inboxda yo‘q. `sale_open` har doim `WaitingForSaleAdapter`; biror customer/sale/stock/event qisman yozilmaydi. Bu to‘liq 2b-2 yakuni emas.
- Authorize/known body/request/device sequence/account ownership bir writer snapshotida tekshiriladi. Frozen payment allocation qayta hisoblanmaydi. Exact replay hech qanday yangi to‘lov yaratmaydi; changed body hatto codec-valid bo‘lsa ham conflict.
- Pending: mavjud `debt_sync_inbox`, first receivedAt va asl body o‘zgarmaydi. Completed: `sync_meta.debt_envelope_v1:<guid>` da SHA256 + newline + full canonical body. Exact relay/export va restart replay uchun saqlanadi; tarmoq ACK emas.
- 128 paket / 32MiB pending payload limiti. Full queue retry/dependency applicationni bloklamaydi; yangi sig‘magan paket distinct exception bilan rad etiladi, eski yozuv o‘chirilmaydi. Unknown/malformed version bu valid inboxga kiritilmaydi; durable unknown quarantine hali yo‘q.
- SQLite/Room testlari: pending/completed restart, retry/concurrency, atomic outer receipt va inbox delete rollback, customer rollback, original relay, permissions, changed body, count/byte quota, corrupt receipt, HELD va opening gate. Lokal Python 23/23 va whitespace PASS.
- Kod commit: `1ab3794406921075d5e9de6f7a5de4280d624646`. [Parity CI 37765703483](https://github.com/akkiS18/LinePOS/actions/runs/37765703483) SUCCESS: 97+92+155 natijalar C#/Kotlinda teng. Birinchi full CI `37765703458`da desktop/core SUCCESS. Yakuniy ko‘rikda Android katta body o‘qishi 65,536 belgilab chunk qilindi; >2MiB paket restart/retry testi qo‘shildi. Android placeholder occurrence order bo‘yicha retry error update parametrlari ham to‘g‘rilandi, test reason yangilanishini tekshiradi. Yakuniy commit `ce991c7ea7d3c0aab407d0fa2d657a85caf5c02b`; [Yakuniy full CI 37766843940](https://github.com/akkiS18/LinePOS/actions/runs/37766843940) SUCCESS: core/regressiya, Windows desktop build, Android build, API26/API35 Room instrumentatsiya. Har emulyatorda 17 ta test o‘tdi. [Yakuniy parity CI 37766843884](https://github.com/akkiS18/LinePOS/actions/runs/37766843884) SUCCESS: 97+92+155 natijalar mos. Fizik qurilma/LAN/UI testlari bu bosqichda bajarilmagan.
- UI/transport/main/release yo‘q; Firebase/CBU o‘zgarmadi. Kontrakt va keyingi scope: [DEBT_INBOX.md](DEBT_INBOX.md).

## 3A-2b-2b tayyorgarligi: Android nasiya chek turi

- Concrete adapter ko‘rigida yangi bloklovchi kamchilik topildi: Room `Converters.toPaymentType("DEBT")` enumda DEBT bo‘lmagani uchun `CASH`ga jim qaytarardi. SQL fixturelarda `DEBT` bor edi, ammo native SaleDao bilan qayta o‘qish tekshirilmagan edi.
- Android `PaymentType.DEBT` qo‘shildi; Room string saqlash formati/sxema o‘zgarmadi. Hisobot/chek labeli `Nasiya`; yangi kassadagi tanlov/tugma qo‘shilmadi. Legacy DEBTdan taxminiy customer/account/event yaratilmaydi.
- `SaleRepository.completeSale` DEBTni tranzaksiyadan oldin rad etadi, hatto cash+card totalga teng yuborilsa ham. Qaytarish turlarining oldingi cheklovi ham shu oldindan tekshiruvga ko‘chirildi.
- `LegacySalePaymentType` mavjud sale-only JSON mappingni ajratadi: DEBT eksporti va numeric 3 / DEBT / string 3 importi rad etiladi, CASHga aylanmaydi. Snapshotda mavjud sale GUID uchun ham tekshiriladi; exception butun snapshot transactionini, jumladan cursorni rollback qiladi. Oddiy to‘lov turlarining mappingi saqlandi. Bu hali to‘liq debt-aware transport yoki restore himoyasi emas.
- Yangi 3 instrumentatsiya testi: native Room DAO restart/filter orqali DEBTni saqlash, haqiqiy cash/card/profit/FX/receipt identity, legacy mapping va oddiy repositoryda qarz daftarisiz savdo yaratishni rad etish. Lokal Python 23/23 va whitespace PASS.
- Kod commit: `eb5e40300772d8dc9c6e03672ec21fcf886367bf`. [Full CI 37831090481](https://github.com/akkiS18/LinePOS/actions/runs/37831090481) SUCCESS: core/regressiya, desktop build, Android build va API26/API35 Room instrumentatsiya. Har emulyatorda 20 ta test, jumladan 3 ta yangi test o‘tdi. Fizik qurilma/LAN/UI testi bu qismda bajarilmadi.
- Limit sabab bu yakunlangan tayyorgarlik qismi alohida checkpoint qilindi. **3A-2b-2b sale/stock adapter va source local freeze hali bajarilmadi**; `WaitingForSaleAdapter` o‘zgarmadi. Nasiya returns/report/backup integratsiyasi ham keyingi reja bosqichlarida qoladi. Firebase/CBUga tegilmadi; main merge/release yo‘q.

## 3A-2b-2b desktop qismi: concrete sale/stock receiver

- `DebtSaleReceiver.cs` va desktop inboxning optional trusted actor/user resolveri: frozen sale/items, stock deltalari, customer/account/event, full-body receipt va pending removal aynan bitta writer tranzaksiyada.
- Desktopda `users` jadvali yo‘q; positive attribution IDni trusted host actor mapping beradi, incoming body yoki default user 1 emas. Mapping/product/warehouse yetishmasa dependency kutadi. Android portda haqiqiy users dependency ham tekshirilsin.
- Asl mahsulot/ombor nomi, kategoriya, birlik, tannarx valyutasi, kurs, komissiya va vaqt saqlanadi. Takroriy product/warehouse satrlari yig‘iladi, boshqa ombor saqlanadi, product aggregate qayta hisoblanadi. Manfiy qoldiq ruxsat etilgan; stock timestamp orqaga ketmaydi.
- Legacy REALga decimal roundtrip yo‘qotishsiz bo‘lishi shart; ±1e18 chegara, lossy intermediate/final balances rad etiladi. Future local source preflight ayni cheklovni oldindan bajarishi shart.
- Asl movement GUIDli HELD `debt_stock` va `debt-sale:<sale-guid>` HELD marker; source user ID + sale hash bog‘lanadi. Exact replay tarixiy header/items/markerlarni tekshiradi, bugungi qoldiqni o‘zgartirmaydi; changed body, operation collision yoki oldindan mavjud unrelated sale rad etiladi.
- Haqiqiy SQLite testlari: missing dependency/retry, historical fields, absent stock row, negative/aggregate stock, restart/concurrent replay, changed body/item/movement, legacy sale collision, unsafe REAL va o‘nta write-boundary rollback. Default inbox opening gate testlari saqlandi. Lokal Python 23/23 va whitespace PASS.
- Kod commit: `3274bda3e0158b3d5ace7e7b12193ad1856718f3`. [Full CI 37910217241](https://github.com/akkiS18/LinePOS/actions/runs/37910217241) SUCCESS: yangi desktop receiver testlari, core/regressiya, Windows desktop build, Android build va API26/API35 instrumentatsiya. Har emulyatorda mavjud 20 ta test o‘tdi. [Parity CI 37910217147](https://github.com/akkiS18/LinePOS/actions/runs/37910217147) SUCCESS: 97 hisob + 92 component + 155 envelope natijalari C#/Kotlinda teng. Android concrete receiver hali yo‘qligi uchun bu uning testi emas; fizik qurilma/LAN/UI testi bajarilmadi.
- Bu faqat desktop receiver yakuni; Android, source local freeze/preflight va 3A-2c tugamadi. Default desktop resolver yo‘q bo‘lsa va Androidda hali `WaitingForSaleAdapter`. UI/main/release yo‘q, Firebase/CBU o‘zgarmadi. Kontrakt: [DEBT_SALE_RECEIVER.md](DEBT_SALE_RECEIVER.md).

## 3A-2b-2b Android qismi: Room concrete sale/stock receiver

- `DebtSaleReceiver.kt`: desktopning frozen sale/items/stock/customer/account/event/full receipt adapteri Android Room writer transactioniga ko‘chirildi. Default gate saqlandi; optional trusted actor/user resolver bilan yoqiladi.
- Androidda mapped positive user ID haqiqiy `users` jadvalida bo‘lishi shart. Sotuvchi, mahsulot yoki ombor yetishmasa `WaitingForDependency`, hech qanday qisman moliyaviy yozuv yo‘q. User 1 fallback yo‘q.
- API26 SQLite uchun UPSERT o‘rniga ayni writer tranzaksiyasida SELECT + INSERT/UPDATE; REPLACE qilinmaydi. Placeholder argument tartibi tekshirildi; BigDecimal scale farqi numeric `compareTo` bilan hisobga olindi. Tarixiy narx/cost/FX/fee, movement GUID, negative/aggregate stock va HELD markerlar desktop kontraktiga mos.
- Yangi Room testi: native DAO/report o‘qish, missing user/product/warehouse, default gate retry, restart/concurrent duplicate, changed body/item/movement, unrelated legacy sale, precision rejection va o‘nta write-boundary rollback. Ikkinchi test 1000 itemli >2MiB paketni to‘liq apply qiladi; restartdan keyin exact relay va duplicate stock ta’siri yo‘qligini tekshiradi.
- Kod commit: `6d362c725ce7dcaed28a4d28ba9df880771ab50d`. Lokal Python 23/23 va whitespace PASS. [Full CI 37913718246](https://github.com/akkiS18/LinePOS/actions/runs/37913718246) SUCCESS: desktop/core regressiyalari, Windows desktop build, Android build va API26/API35 Room instrumentatsiya. Har emulyatorda 22 ta test, jumladan yangi concrete receiver va >2MiB/1000-item testi o‘tdi. [Parity CI 37913718371](https://github.com/akkiS18/LinePOS/actions/runs/37913718371) SUCCESS. Fizik qurilma/LAN/UI testi bu bosqichda bajarilmadi.
- Ikkala receiver tayyor bo‘lgach **Qism 1 (3A-2b-2b source local envelope freeze va preflight)** to‘liq amalga oshirildi.

## Qism 1 natijasi: jo‘natuvchida o‘zgarmas paketni atomik saqlash va preflight

- C# va Kotlin `DebtRepository`da yangi `OpenSale(DebtOpenSaleCommand)` API joriy etildi. Arbitrary callback o‘rniga to‘liq muzlatilgan snapshot (`DebtSaleSnapshot`) qabul qilinadi; stock deltalari (`-quantity`), movement GUIDlar, UZS/USD kursi, karta komissiyasi va REAL/decimal chegaraviy qiymatlari (±1e18, aniq tiyin, max 1000 items, max 6MiB envelope) local commitdan oldin preflight qilinadi.
- Bitta writer tranzaksiyasida: `sales`, `sale_items`, `product_stocks`, `products.stock_quantity`, `sync_journal` (original `debt_stock` va `debt-sale:<guid>` markerlari, `acked=-1`, `group_id=cmd.RequestGuid`), `debt_events` (`sale_open`), `debt_accounts`, `debt_command_receipts`, outbox va `sync_meta`da `"debt_wire_v1:event:<guid>"` hamda `"debt_envelope_v1:<guid>"` muhrlanadi. Shuningdek, commitdan oldin `DebtSaleReceiver.Verify` / `verify` chaqirilib, receiver kutgan barcha maydonlar 100% mosligi tasdiqlanadi.
- `CreateCustomer` va `TakePayment` ham o‘z komponenti va envelope paketini (`debt_envelope_v1:<guid>`) shu biznes tranzaksiyasida `sync_meta`ga muhrlaydi. Natijada `DebtEnvelopeInbox.ExportApplied(requestGuid)` jo‘natuvchi qurilmaning o‘zida ham tashqi navbatlarsiz to‘liq muzlatilgan paketni darhol eksport qila oladi.
- Jo‘natuvchiga qaytgan echo (`inbox.Receive(wire, at)`): avval saqlangan `sync_meta` muhrini tekshirib, `AlreadyApplied` qaytaradi va pul yoki omborga ikkinchi marta aslo tegmaydi.
- Yangi C# test to‘plami (`DebtSourceEnvelopeTests.cs`): local commit → export → peer receive → source echo (sale, payment, customer); restart/retry bir xil baytlar; concurrent double submit; historical metadata immutability; xatoda 10 ta jadvalning to‘liq rollbacki; >1000 items va unsafe REAL preflight rad etilishi tekshirildi. Barcha 9 ta C# test to‘plami muvaffaqiyatli o‘tdi.
- Yangi Android instrumentatsiya testi (`DebtSourceEnvelopeTest.kt`): Room writer tranzaksiyasi, `openSale`, `exportApplied`, peer receive, source echo, restart, rollback, preflight hamda Room native DAO `saleDao().getSaleWithItemsById` orqali `PaymentType.DEBT` o‘qilishi to‘liq tekshirildi. Android Kotlin va test kodlari xatosiz kompilyatsiya qilindi.
- Lokal Python testlari: 23/23 PASS. `git diff --check` PASS. Desktop Release build: 0 Warning, 0 Error.
- UI/LAN transport hali ulanmagan; main merge/release yo‘q; Firebase/CBU o‘zgarmadi.

## Qism 2 natijasi: LAN transport, capability, push/pull va ACK (3A-2c)

- Desktop `LocalSyncServer.cs`: `capabilities` ichida `"debtLedgerV1"` va `storeGuid` e’lon qilinadi (`/api/v2/pair` va `/api/ping`).
- Yangi endpointlar:
  - `POST /api/v2/debt/push`: max 500 envelope / 8MiB hajm tekshiruvi; har paket `_debtInbox.Receive` orqali qayta ishlanadi; xatolar ushlanib, `DrainPending()` bajariladi; javobda har envelope GUID va uning holati (`Applied`, `AlreadyApplied`, `WaitingForDependency`, `WaitingForSaleAdapter`, `InboxFull`) qaytariladi.
  - `GET /api/v2/debt/pull?cursor=<cursor>`: `sync_journal`dan `debt_*` guruhlari saralanadi; 250 ta envelope / 6MiB hajm bilan chegaralanadi; `_debtInbox.ExportApplied` yordamida muhrlangan kanonik baytlar chiqariladi va cursor siljitiladi.
  - `GET /api/sync/download_db`: bazada faol `debt_customers` yoki `debt_events` yozuvi bo‘lsa HTTP 400 bilan rad etiladi; `sync_meta` va qarz muhrlarini tozalanishdan himoyalaydi.
- Legacy izolatsiya: `WifiSyncStore.Pull` so‘rovida `sales WHERE payment_type <> 3` qilinib, nasiya savdolari eski qurilmalarga oddiy savdo bo‘lib oqib ketishi to‘xtatildi; `WifiSyncStore.Push` legacy savdo uchun `PaymentType == 3` bo‘lsa qat’iy rad etadi.
- Android `LocalSyncManager.kt`:
  - `pairDesktop` va `pingDesktop` da peer server `capabilities` ichida `"debtLedgerV1"` mavjudligi va `storeGuid` to‘g‘riligi tekshirilib, `debt_scope` ga biriktiriladi.
  - `syncDebtIfSupported`:
    - Push: `sync_journal`dan `kind LIKE 'debt_%' AND acked = -1` bo‘lgan guruhlar aniqlanadi; `exportApplied` orqali kanonik paketlar `/api/v2/debt/push` ga yuboriladi. Faqat `Applied` yoki `AlreadyApplied` holatidagi guruhlar uchun `sync_journal SET acked = 1` va `saleDao.markSalesSyncedByGuids` bajariladi.
    - Pull: `/api/v2/debt/pull?cursor=$debtCursor` dan paketlar olinadi; Room `database.withTransaction` ichida `inbox.receive` qilinadi va `debt_cursor` atomik tarzda yangilanadi.
    - Drain: yetishmagan dependency sabab kutayotgan paketlar `inbox.drainPending` bilan avtomatik yakunlanadi.
    - Izolatsiya: Qarz sinxroni legacy mahsulot ziddiyatlari (`sync_conflicts`) tekshiruvidan oldin ishga tushadi; narx/mahsulot mojarosi mustaqil qarz to‘lovlari va savdolarini to‘xtatmaydi.
- Testlar:
  - C# LAN integratsiya testlari (`tests/WifiSync.CoreTests/Program.cs`): 20/20 PASS. Barcha holatlar (capability/ping, legacy push/pull rad etilishi, unauthenticated 401, wrong server 400, customer envelope push va AlreadyApplied retry, tampered body rad etilishi, active debt download_db rad etilishi, pull cursor advance, sale envelope push va ombor kamayishi, AlreadyApplied takroriy zaxira kamaytirmasligi, payment envelope push va balans qisqarishi, delta pull, orphan dependency kutish va root kelganda avtomatik drain, conflict isolation) tekshirildi.
  - C# Core test to‘plamlari (`tests/Business.CoreTests`): 9/9 PASS.
  - Python testlari (`tests/test_*.py`): 23/23 PASS.
  - Desktop Release build: 0 Warning, 0 Error.
  - Android yangi instrumentatsiya testi (`DebtLanSyncTest.kt`): Mock desktop server orqali `LocalSyncManager` push, pull, ACK, cursor va legacy conflict isolation jarayonlari to‘liq qamrab olindi.
  - Android Kotlin va AndroidTest kompilyatsiyasi: SUCCESS.
- Tekshirilgan kod commit: `2a4af27`.
- UI/Cashier tugmalari ulanmagan; main merge/release yo‘q; Firebase/CBU o‘zgarmadi.

## Qism 3 natijasi: bir nechta qurilma, tiklash epochlari va kontaktlar (3B-1/3B-2)

- Kontaktlar boshqaruvi va optimistik konkurentlik:
  - C# va Kotlin `DebtRepository`da `DebtCustomerRecord`, `DebtCustomerUpdate`, `ReadCustomer`, `UpdateCustomer` (versiya ziddiyatini tekshiruvchi optimistic concurrency: `revision == currentRev`, aks holda `DebtConflictException`), hamda `ArchiveCustomer` (`archived = 1/0`, revision oshishi) to‘liq joriy qilindi.
  - Arxivlangan mijozga yangi nasiya ochish (`OpenSale`) va mahalliy kassadan to‘lov qabul qilish (`TakePayment`) qat’iy bloklandi. Ammo avval ochilgan qarz hisoblari faol qoladi va tarmoq orqali boshqa qurilmalardan kelgan oflayn to‘lovlar (`Inbox.Receive`) hech qanday xatosiz qabul qilinib, hisob balansini kamaytiradi.
  - Tarixiy savdolardagi `customer_name_at_sale` mijoz ismi keyinchalik tahrirlanganda ham o‘zgarmas (immutable) bo‘lib qoladi.
- Tarixiy tiklash epochi (history epoch) va cursor rollback:
  - Desktop `LocalSyncServer`da `_debtHistoryEpoch` (bazaning `sync_meta.debt_history_epoch` kaliti) o‘rnatildi va `/api/ping`, `/api/v2/pair`, hamda `/api/v2/debt/pull` da e’lon qilinadi.
  - `/api/v2/debt/pull` da so‘ralgan `cursor` noto‘g‘ri (manfiy yoki mavjud eng yuqori cursordan katta) bo‘lsa, server `{ "cursorRollback": true, "cursor": 0, "historyEpoch": ..., "envelopes": [] }` qaytaradi.
  - Android `LocalSyncManager`: juftlashuvda `debt_history_epoch` saqlanadi. Sinxronizatsiya vaqtida serverning `historyEpoch`i lokal saqlangandan farq qilsa yoki serverdan `cursorRollback` kelsa: lokal `debt_cursor` nollanadi, avval serverga yuborilib tasdiqlangan (`acked = 1`) barcha qarz guruhlari qayta pending (`acked = -1`) holatiga o‘tkaziladi. Shu bilan birga barcha mavjud pending yozuvlar saqlanadi va serverga qayta push qilinadi; natijada tiklangan serverga barcha yetishmayotgan paketlar qayta yetkaziladi, pul va tovar zaxirasi esa aslo dublikat qilinmaydi.
- Mustaqil writer epochlar va zaxira klonlari:
  - Baza zaxirasidan nusxalangan klon qurilmalar `device_guid` qayta generatsiya qilinishi va har repository yangi writer UUID epoch bilan ishga tushishi sababli bir-biri bilan sequence to‘qnashuviga uchramaydi.
- Testlar:
  - C# multi-device integratsiya test to‘plami (`tests/Business.CoreTests/DebtMultiDeviceTests.cs`):
    - 3-qurilma topologiyasi (Desktop + 2 Telefon) konvergentsiyasi: savdo va to‘lovlar har xil tartibda yetib kelganda yakuniy qoldiq (10,000 UZS), kassa tushumi (70,000 UZS), ombor qoldig‘i (18 dona) va eventlar soni (4 ta) aynan bir xil bo‘lishi tasdiqlandi.
    - Oflayn ortiqcha to‘lov (offline excess payment): ikkita oflayn qurilmada bir vaqtda to‘liq qarz to‘langanda pul yozuvlari yo‘qolmaydi (jami 200,000 UZS kassa), qarz to‘liq yopiladi (0) va mijoz hisobida 100,000 UZS haqdorlik (-10,000,000 tiyin credit) shakllanadi.
    - Klonlangan baza writer epoch izolatsiyasi: klon nusxalarda alohida `device_guid` va mustaqil 1 dan boshlanuvchi ketma-ketlik kafolatlandi.
    - Server zaxirasiga qaytish (restore rollback): server eski holatiga qaytganda mijoz cursor ziddiyatini anglab, o‘z lokal pendinglarini yo‘qotmasdan qayta sinxronizatsiya qilishi tekshirildi.
    - Mijoz GUID izolatsiyasi: bir xil ism va telefonli yangi mijozlar mustaqil hisoblar ochadi.
    - Mijoz tahriri va arxivlanishi: versiya ziddiyati (optimistic concurrency), arxivlanganda yangi savdo taqiqlanishi va oflayn peer to‘lovining qabul qilinishi tasdiqlandi.
  - Android yangi instrumentatsiya testi (`DebtMultiDeviceConvergenceTest.kt`): Room writer tranzaksiyasi, 3 qurilma konvergentsiyasi, klon writer izolatsiyasi va arxivlash semantikasi to‘liq qamrab olindi.
  - Barcha testlar: C# `Business.CoreTests` 10/10 PASS, C# `WifiSync.CoreTests` 20/20 PASS, Python testlari 23/23 PASS, Desktop Debug build: 0 xato, Android compileDebugKotlin va compileDebugAndroidTestKotlin: SUCCESS.
- Tekshirilgan kod commit: `27231d6`.
- UI/Cashier hali ulanmagan; main merge/release yo‘q; Firebase/CBU o‘zgarmadi.

## Qism 4 natijasi: Desktop frontend va kassa integratsiyasi (4)

- Desktop Qarz xizmati (`PosElectro.Desktop/Debt/DebtService.cs`):
  - `DebtRepository` ustiga xavfsiz va UI-dan ajratilgan (decoupled) application service qatlami qurildi.
  - Xizmat o‘z konstruktorida `InstallSyncSchema(conn)` va `DebtSchema.Install(conn)` orqali barcha zaruriy metadata (`sync_meta`, `sync_control`, `sync_journal`) va qarz jadvallarini kafolatlaydi.
  - Barcha hisob-kitoblar qat'iy tiyin (minor units, 1 UZS = 100 tiyin) da yuritiladi.
  - `GetSummary()`: jami faol qarz, qarzdorlar soni, muddati o‘tgan qarzlar, ortiqcha to‘lovlar (kredit/haqdorlik) va umumiy to‘langan summani hisoblaydi.
  - `GetCustomerList(search, filter)`: mijozlarni qidiruv (ism va telefon) hamda 5 ta filtr bo‘yicha ajratadi (`ActiveDebt`, `Overdue`, `Settled`, `Credit`, `All`).
  - `GetCustomerDetails(customerGuid)`: mijoz ma'lumotlari, uning barcha nasiya hisoblari (`Accounts`) va to‘liq o‘zgarmas moliyaviy voqealar xronologiyasi (`Events`).
  - `PreviewPayment(customerGuid, paymentMinor, targetAccountGuid)`: to‘lov summasi kiritilganda ortiqcha to‘lovni (overpayment) input darajasida bloklaydi, oldest-first FIFO yoki tanlangan hisob bo‘yicha jonli taqsimot satrlarini (`Lines`) hisoblab beradi.
  - `RecordPayment(...)`: to‘lovni atomik qabul qiladi; takroriy yuborishda (retry/replay) avvalgi `occurred_at` ni saqlab, `DebtRepository.Replay` bilan 100% idempotent ishlaydi.
  - `BuildSaleSnapshot(...)` va `OpenDebtSale(...)`: kassa savatchasi elementlarini (`DebtCartItemDto`) qat'iy canonical UUIDlar, `StockDelta = -quantity`, tannarx valyutasi va kurs bilan muzlatilgan snapshotga aylantiradi; manfiy qoldiq (negative stock) ruxsatini saqlaydi.
  - `CreateCustomer`, `UpdateCustomer` (versiya nazorati bilan) va `ArchiveCustomer` integratsiyasi.

- Kassa integratsiyasi (`PosElectro.Desktop/ViewModels/CashierViewModel.cs` & `Views/CashierView.xaml`):
  - 4-to‘lov turi: "📒 Nasiya" (`SelectedPaymentType == 3`).
  - Nasiya tanlanganda kassa oynasida mijoz tanlash (qidiruvli ComboBox + "+ Yangi" tezkor mijoz qo‘shish modali), mijozning oldingi qarzi, avans to‘lovlari (naqd/karta), qoladigan qarz nishoni (badge) va to‘lash muddati (due date) ko‘rsatiladi.
  - Hold savatlar (`HeldCartModel`): har bir kutishdagi savat o‘zining tanlangan mijozi, muddati va avans summasini mustaqil saqlaydi va qayta tiklaydi.
  - `ConfirmSale()`: Nasiya savdolarini `_debtService.OpenDebtSale` orqali to‘liq atomik va xavfsiz amalga oshiradi; bazaga dublikat yozilishining oldi olindi; takroriy bosishdan himoyalovchi `_isSubmittingSale` kiritildi; chek printeri yoki kvitansiya xatolari moliyaviy tranzaksiyaga ta'sir qilmaydi (decoupled error handling).

- Qarzlar boshqaruvi oynasi (`PosElectro.Desktop/ViewModels/DebtsViewModel.cs` & `Views/DebtsView.xaml`):
  - Zamonaviy Dark Theme Apple/Fluent UI dizayni: 4 ta KPI kartasi, qidiruv paneli va 5 ta filtr chiplari.
  - Chap tomonda mijozlar ro‘yxati (balans nishoni, muddati o‘tganlik ogohlantirishi, arxiv holati), o‘ng tomonda tanlangan mijozning batafsil paneli (Nasiyalar va Tarix tablari).
  - Yangi mijoz qo‘shish va tahrirlash modali (telefon va izoh ixtiyoriy, ism majburiy, ziddiyat tekshiruvi).
  - Qarz to‘lovini qabul qilish modali: Naqd / Karta / Aralash to‘lov, jonli preview va tugmani o‘chirib qo‘yish (submit disable).
  - "🛒 Kassada yangi nasiya ochish" tugmasi: mijoz tanlangan holda to‘g‘ridan-to‘g‘ri Kassaga o‘tish va savatni saqlab qolish.

- Asosiy oyna navigatsiyasi (`MainWindow.xaml` & `MainViewModel.cs`):
  - Menyu: **Kassa / Ombor / 📒 Qarzlar / Hisobotlar / Wi-Fi Sinxron**. Tablar orasida erkin va silliq o‘tish.

- Testlar:
  - C# Desktop integratsiya test to‘plami (`tests/Business.CoreTests/DebtDesktopIntegrationTests.cs`):
    - Service summary, customer revision concurrency, debt sale snapshot, manfiy ombor qoldig‘i (10 - 15 = -5), cashier hold/open, idempotency replay, payment allocation preview, ortiqcha to‘lov bloklanishi, qarzni to‘liq yopish (settle) va arxivlash.
  - Barcha testlar:
    - C# `Business.CoreTests`: 11/11 test to‘plamlari PASS (100%).
    - C# `WifiSync.CoreTests`: 20/20 testlar PASS (100%).
    - Python `test_sync_schema.py`: 7/7 testlar PASS.
    - `PosElectro.Desktop.csproj` kompilatsiyasi: 0 xato, 0 ogohlantirish.
- Tekshirilgan kod commit: `2c6c433`.

## Qism 5 natijasi: Mobil frontend va offline kassa integratsiyasi

- `DebtService.kt`:
  - Room/SQLite ustida do'konga bog'langan (`debt_scope`) servis qatlami; legacy warehouse GUIDni `00000000-0000-0000-0000-000000000001` ga xavfsiz moslash;
  - KPI summary, 5 ta filtrli mijozlar ro'yxati, hisoblar (accounts) va voqealar (events) tarixi;
  - Ortiqcha to'lovni bloklovchi live allocation preview (`previewPayment`);
  - Baytma-bayt idempotent replay bilan to'lov yozish (`recordPayment`);
  - `buildSaleSnapshot` (manfiy stock ruxsati, minor UZS pul birliklari, aniq tiyin);
  - Atomik oflayn nasiya savdosini ochish (`openDebtSale`);
  - Mijozlar CRUD va arxivlash.

- Kassa va to'lov integratsiyasi (`CashierViewModel.kt`, `CashierScreen.kt`, `CheckoutPaymentDialog.kt`, `HoldCartsDialog.kt`):
  - 4-to'lov turi "Nasiya" (2x2 grid: Naqd, Karta, Aralash, Nasiya);
  - Mijozni qidirib tanlash modali (`CustomerPickerDialog`), "+ Yangi" tezkor mijoz ochish modali (`QuickAddCustomerDialog`);
  - Savatda faol nasiya mijozi ko'rsatkichi va bekor qilish;
  - Muzlatilgan savatlarda (`HeldCart`) nasiya mijozi va avanslarni saqlash hamda `HoldCartsDialog` da "📒 {Mijoz} (Nasiya)" nishoni;
  - Naqd va karta avanslari, qolgan nasiya summasi va to'lov muddati;
  - `completeDebtSale` orqali atomik oflayn nasiya yopish (`checkoutGate` bilan himoyalangan).

- Qarz daftari UI (`DebtsViewModel.kt` & `DebtsScreen.kt`):
  - 4 ta KPI kartasi (Jami nasiya, Muddati o'tgan, Jami to'langan, Haqdorlik);
  - 5 ta filtr chiplari ("Barchasi", "Qarzdorlar", "Muddati o'tgan", "To'langan", "Arxiv") va qidiruv;
  - Mijozlar ro'yxati, mijoz balansi va muddati o'tganlik nishoni;
  - Mijoz tanlanganda to'liq tafsilotlar: Nasiyalar (Accounts) va Tarix (Events) tablari;
  - Jonli taqsimot ko'rsatuvchi "To'lov olish" modali (`PaymentCollectionDialog`);
  - Tanlangan mijoz bilan kassaga o'tish ("Kassada ochish") handoff'i;
  - Mijoz qo'shish / tahrirlash / arxivlash modali.

- Asosiy oyna navigatsiyasi (`MainScreen.kt`):
  - Apple HIG uslubidagi pastki dock 4 tadan 5 ta tabga kengaytirildi: **Kassa / Ombor / 📒 Qarzlar / Hisobotlar / Sozlamalar**;
  - TopAppBar sarlavhasi ("SMART — Qarz daftari") va BackHandler moslashtirildi.

- Testlar va verifikatsiya:
  - Yangi instrumentatsiya testi `DebtMobileIntegrationTest.kt`:
    - Mijoz lifecycle va aktiv/arxiv filtrlari;
    - Oflayn nasiya savdosi (Initial stock: 2.0, Sold: 5.0 -> Remaining: -3.0 manfiy stock ruxsati saqlandi);
    - Naqd va karta avanslari to'g'ri qayd etilishi;
    - HeldCart nasiya maydonlari saqlanishi va tiklanishi;
    - To'lov preview, to'lov yozish, hisoblarning FIFO yopilishi va baytma-bayt idempotency replay;
  - Kotlin va Android test kompilyatsiyasi:
    - `compileDebugKotlin`: BUILD SUCCESSFUL (0 xato).
    - `compileDebugAndroidTestKotlin`: BUILD SUCCESSFUL (0 xato).
  - Regressiya testlari:
    - C# `Business.CoreTests`: 11/11 test to‘plamlari PASS (100%).
    - C# `WifiSync.CoreTests`: 20/20 testlar PASS (100%).
    - Python `tests/test_sync_schema.py`: 7/7 testlar PASS.
- Tekshirilgan kod commit: `6ef03f8`.

## Qism 6 natijasi: Return, reversal, refund va kredit transferi
- Desktop Core va Returns integratsiyasi (`desktop/PosElectro.Desktop/`):
  - `DebtRepository.cs`: `ReversePayment`, `RefundCredit`, `TransferCredit` metodlari va komandalari (`DebtPaymentReversalCommand`, `DebtCreditRefundCommand`, `DebtCreditTransferCommand`) tranzaksiyaviy delta va single-reversal cheklovlari bilan qo'shildi.
  - `Returns/ReturnStore.cs`: `ReturnQuote`ga qarz hisobi va balansi ulandi; `Commit`da `DebtAccounting.DebtReturnSplit` hisoblanib, qarz kamaytirilishi (`return_offset` event) va naqd/karta refundi bitta tranzaksiyada commit qilinadi. Quote-to-commit o'rtasidagi race condition tekshiruvi joriy etildi.
  - `Returns/ReturnReversal.cs`: Qarz offseti bo'lgan qaytarishni bekor qilishda asl offset deltasini to'liq teskari qilib (`+offset`), bitta qaytarish yagona reversal bilan bekor qilinishi ta'minlandi.
  - `Debt/DebtService.cs`: `ReversePayment`, `RefundCredit`, `TransferCredit` servis qatlamiga ulandi; DTO'larda `IsReversed`, `CanReverse` va `KindDisplay` maydonlari kengaytirildi.
  - `Services/LocalSyncServer.cs`: LAN authority HTTP endpointlari qo'shildi (`/api/v2/debt/reverse_payment`, `/api/v2/debt/refund_credit`, `/api/v2/debt/transfer_credit`).
- Desktop UI (`PosElectro.Desktop/`):
  - `ViewModels/DebtsViewModel.cs`: `HasCredit`, `HasActiveDebt`, `CanTransferCredit` hisoblandi; `ReversePaymentCommand` xavfsiz tasdiqlash dialogi bilan, `OpenRefundCreditModalCommand` va `TransferCreditCommand` kiritildi.
  - `Views/DebtsView.xaml`: Headerda "💸 Pulni qaytarish", "🔁 Qarzga o'tkazish" tugmalari, to'lovlar jadvalida har bir yozuv yonida "Bekor qilish" tugmasi hamda kreditni qaytarish modali qo'shildi.
- Android Core va UI (`app/src/main/java/uz/pos/electro/`):
  - `data/debt/DebtRepository.kt` va `DebtService.kt`: `reversePayment`, `refundCredit`, `transferCredit` amalga oshirildi.
  - `data/sync/LocalSyncManager.kt`: Kompyuter bilan LAN authority orqali bog'lanuvchi `reversePaymentOnDesktop`, `refundCreditOnDesktop`, `transferCreditOnDesktop` metodlari yaratildi.
  - `ui/reports/ReturnDialog.kt`: Qarzli chek qaytarilganda qarzdan chegiriladigan summa va mijozga to'lanadigan summa ajratib ko'rsatildi va kassa xatoliklaridan himoyalandi.
- Testlar va verifikatsiya:
  - C# `Business.CoreTests` yangi D13–D16 test to'plami (`DebtReturnAndReversalTests.cs`):
    - D13: 100k savdo / 20k to'lov / 80k qarz: 30k qaytarish -> qarz 50k / refund 0; 70k qaytarish -> qarz 0 / refund 20k — PASS.
    - D14: Quote va Commit o'rtasidagi balance poygasi (race condition) xatolik bilan rad etilishi — PASS.
    - D14: Kech kelgan oflayn to'lov sinxronlashganda eski returnni buzmasdan, xavfsiz kredit hosil qilishi — PASS.
    - D15: Payment reversal asl to'lov summasi va fee'ni teskari qilishi, takroriy reversal rad etilishi — PASS.
    - D16: Credit refund faqat mavjud haqdorlik chegarasida ishlashi, ortiqcha refund bloklanishi; Credit transfer manbadan nishon qarzga naqdsiz o'tishi — PASS.
    - Natija: `Business.CoreTests` 12/12 test to'plamlari 100% PASS.
  - C# `WifiSync.CoreTests`: 20/20 testlar PASS (100%).
  - Python testlari: 23/23 testlar PASS (100%).
  - Desktop loyihasi: `net8.0-windows` 0 xato, 0 ogohlantirish bilan build bo'ldi.
  - Android loyihasi: `compileDebugKotlin` va `compileDebugAndroidTestKotlin` 0 xato bilan BUILD SUCCESSFUL.
- Tekshirilgan kod commit: `90c7a2a`.

## Qism 7 natijasi: Hisobotlar, foyda, Excel va chek/ko‘chirma
- Buxgalteriya va P&L intizomi (`SaleAccounting.cs` va `SaleAccounting.kt`):
  - Savdo sanasida to'liq revenue/cost/profit bir marta hisoblanadi.
  - Qarz to'lovi (`TakePayment`) savdo daromadi deb hisoblanmaydi (`RevenueMinor = 0`); u faqat kassa pul oqimi (cashflow) va undirilgan kundagi bank komissiyasini hosil qiladi.
  - Nasiya tovar qaytarilganda (`return_offset`) savdo daromadi kamaytiriladi, lekin pul chiqimi (refund) bo'lmagani uchun cashflow o'zgarmaydi.
- Davriy hisobotlar va tahliliy proyeksiyalar (`DebtReportProjection.cs` va `DebtReportProjection.kt`):
  - Davr chegarasi qat'iy `[start inclusive, end exclusive)` mahalliy vaqt bo'yicha;
  - `opening + periodChanges = closing` matematik balansi kafolatlandi;
  - Faol qarzdorlik (`ClosingDebtUz`) va ortiqcha to'lovlar/haqdorlik (`ClosingCreditUz`) aslo bir-biri bilan to'qnashtirilib (netting) yashirilmaydi, alohida ko'rsatiladi;
  - `GetDebtPeriodSummary` va `GetDebtCustomerHistory`: yangi nasiya, undirilgan naqd/karta qarzlar, to'langan bank komissiyasi, boshlang'ich va yakuniy qoldiqlar.
- Desktop UI va Excel eksporti (`PosElectro.Desktop`):
  - `ReportsViewModel.cs` va `ReportsView.xaml`: "QARZ VA PUL OQIMI (CASHFLOW)" kartalari (Berilgan Nasiya, Undirilgan Qarz, Kassa Pul Oqimi, Faol Qarzdorlik / Haqdorlik);
  - `ExcelExportService.cs`: "QARZ VA CASHFLOW (PUL OQIMI)" analitik bloki va savdolar jadvalida "Nasiya (so'm)" ustuni qo'shildi;
  - `PrinterService.cs` va `DebtsViewModel.cs`: Mijoz batafsil sahifasida "🖨️ Ko'chirma" tugmasi orqali to'liq sverka aktini 80mm/58mm chek printeriga chiqarish yoki clipboardga nusxalash imkoniyati yaratildi.
- Android UI va Excel eksporti (`app/src/main/java/uz/pos/electro/`):
  - `DebtReceiptFormatter.kt`: 32-ustunli hisob-kitob ko'chirmasi matn formateri;
  - `DebtService.kt`: `getDebtPeriodSummary` Room/SQLite so'rovi;
  - `ReportsViewModel.kt` va `ReportsScreen.kt`: Hisobotlar ekraniga "Qarz va Kassa Pul Oqimi" kartasi qo'shildi;
  - `ExcelExporter.kt`: Excelga Qarz va Cashflow bo'limi hamda "Nasiya (so'm)" ustuni integratsiya qilindi;
  - `DebtsScreen.kt`: "Ko'chirmani ulashish (Sverka)" tugmasi orqali mijoz ko'chirmasini Telegram/SMS orqali ulashish (share intent) kiritildi.
- Testlar va verifikatsiya:
  - C# `Business.CoreTests` yangi D17–D20 va D26 test to'plami (`DebtReportTests.cs`):
    - D17: Bugun nasiya / ertaga to'lov -> savdo kuni revenue bor/cashflow 0, ertaga revenue 0/cashflow to'liq; fee ikki marta olinmasligi — PASS.
    - D18: Oldingi davr qarzi yopilganda savdo daromadi 0 bo'lishi; safe credit holati — PASS.
    - D19: Midnight va davriy filtrlar `[start, end)` aniq ishlashi — PASS.
    - D20: Excel eksporti va UI hisoboti bir xil davriy formuladan foydalanishi — PASS.
    - D26: Mijoz hisob-kitob ko'chirmasi (Statement) to'liq formati — PASS.
    - Barcha test to'plamlari: `Business.CoreTests` 13/13 test to'plamlari 100% PASS.
  - C# `WifiSync.CoreTests`: 20/20 testlar PASS (100%).
  - Python testlari: 23/23 testlar PASS (100%).
  - Desktop loyihasi: `net8.0-windows` 0 xato, 0 ogohlantirish bilan build bo'ldi.
  - Android loyihasi: `compileDebugKotlin` va `compileDebugAndroidTestKotlin` 0 xato bilan BUILD SUCCESSFUL.

## Keyingi bosqich — Qism 8: Backup/restore, regressiya va yakuniy sinov
1. Snapshot bir tranzaksiyada barcha qarz jadvallarini (`debt_schema`, `debt_scope`, `debt_customers`, `debt_accounts`, `debt_events`, `debt_event_lines`, `debt_command_receipts`, `debt_sync_inbox`, `sync_journal`, `sync_meta` seallari) qamrab olishi.
2. Faqat to'lov bo'lgan kun, uzilgan holatdagi lokal amallar, missing dependency inbox va >2MiB applied body bilan restore sinovi.
3. Yangi writer epoch va server history/cursor reconciliation tiklangan nusxani eski amallarni qayta yozmasligini tekshirish.
4. D01–D27 bo'yicha to'liq yakuniy test va regressiya verifikatsiyasi.

## Muhim cheklovlar

- Firebase/CBU o‘zgarmaydi; manfiy qoldiq ruxsat etilgan.
- Asosiy qarz/payment offline yakunlanadi; returns/correction/refund/credit transfer LAN authorityda qoladi.
- Ikki uzilgan qurilmada ortiqcha undirishni to‘liq bloklash mumkin emas; pul yozuvlari yo‘qolmasin, excess alohida ko‘rinsin.
- Sale profitni debt collection bilan ikki marta hisoblama. Cashflow, receivable va revenue alohida.
- Eski qog‘oz qarz import qilinmaydi; legacy DEBT enumdan taxminiy mijoz qarzi yaratma.
- To‘liq D01–D27 reja testlari o‘tgan deb yozma.
- UI 4/5 tugashi release tayyor degani emas; returns/report/backup integratsiyasi va regressiya gates kerak.

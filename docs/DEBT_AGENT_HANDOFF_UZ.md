# LinePOS qarz daftari — yangi agent uchun ishni davom ettirish qo‘llanmasi

Tayyorlangan sana: 2026-10-09. Bu hujjat qolgan **8 ta ish qismini** tugatish uchun topshirish qo‘llanmasi. 8 qism — 8 sessiya degani emas. Mavjud ishlagan kodni qayta yozish emas, quyidagi tekshirilgan nuqtadan davom ettirish kerak.

## 0. Qayerdan boshlash kerak

- Repository: https://github.com/akkiS18/LinePOS
- Ish branchi: `feature/customer-debt`. Bajarilmagan debt funksiyasini `main`ga merge qilmang.
- Ushbu handoffdan oldingi checkpoint: `4c2a43e3ecef0c7ffbb0927ef1165cfd709d2e27`.
- Oxirgi runtime commit: `6d362c725ce7dcaed28a4d28ba9df880771ab50d` — Android concrete receiver.
- Desktop receiver: `3274bda3e0158b3d5ace7e7b12193ad1856718f3`.
- Handoff keyinroq commit qilinadi; yuqoridagi eski checkpointga reset qilmang. Remote branchning eng yangi HEADini tekshiring.
- Eski suhbatga kirish shart emas. Hujjatlar va amaldagi kod asosiy manba.

Avval `git status`, branch va remote HEADni tekshiring. Foydalanuvchining lokal push qilinmagan o‘zgarishlarini yo‘qotmang, ustidan yozmang. Zarur bo‘lsa alohida worktree ishlating. `reset --hard`, majburiy push yoki foydalanuvchi o‘zgarishlarini avtomatik stash qilish bilan davom etmang. Toza, tegishli checkoutda `git fetch origin` va `git switch feature/customer-debt` / `git pull --ff-only` ishlatilishi mumkin; dirty checkout yoki divergence bo‘lsa avval sababini aniqlang.

O‘qish tartibi:

1. `AGENT.md` — loyiha cheklovlari va tarix.
2. `docs/DEBT_CHECKPOINT_UZ.md` — eng yangi haqiqiy holat va CI dalillari.
3. Ushbu qo‘llanmadagi navbatdagi qism.
4. `docs/DEBT_PLAN_UZ.md` — tasdiqlangan biznes/UI kontrakti va D01–D27 testlari.
5. `docs/DEBT_REPOSITORY.md`, `DEBT_DB_BRIDGE.md`, `DEBT_WIRE.md`, `DEBT_ENVELOPE.md`, `DEBT_INBOX.md`, `DEBT_SALE_RECEIVER.md` — tegishli API kontraktlari.

**Tarixiy matnlar bo‘yicha ogohlantirish:** original rejaning ayrim bo‘limlarida “Room13”, “sxema yaratilmagan” kabi reja yozilgan vaqtdagi holat bor. Hozir Android Room **14**, debt schema **1**. Amaldagi kod va so‘nggi checkpoint ustuvor. Mavjud migratsiyani yoki canonical wire formatini eski reja jumlasiga moslashtirish uchun o‘zgartirmang.

### Hozir tayyor bo‘lganlar

| Qatlam | Haqiqiy holat |
|---|---|
| Hisoblash | C#/Kotlin minor-unit arifmetika, allocation, excess, return/reversal/refund/transfer hisob funksiyalari |
| Baza | Debt schema, migratsiya, oldindan backup, local customer/sale/payment repository va request replay |
| Canonical format | Customer/event va frozen sale/items/stock envelope, hash, qat’iy validatorlar |
| Qabul | Durable inbox; customer/payment hamda desktop/Android concrete sale receiver |
| Replay | Exact body, tarixiy header/items, movement GUID va durable receipt tekshiruvlari |
| Android DEBT | Native Room modeli bor; nasiya CASHga jim aylantirilmaydi; oddiy checkout/legacy JSON cheklangan |
| Hali yo‘q | To‘liq local source freeze, haqiqiy debt transporti, frontend, authority tuzatish integratsiyasi, yakuniy report/backup integratsiyasi |

Receiver constructorida trusted `resolveActorUser` berilmasa opening `WaitingForSaleAdapter` bo‘lib qoladi. Resolver borligi UI/transport ishga tayyor degani emas. Desktopda `users` jadvali yo‘q; host positive attribution ID beradi. Android shu mappingdan tashqari userning haqiqiy jadvalda mavjudligini ham tekshiradi. Incoming paketdagi raqamni avtomatik user ID sifatida ishlatmang.

Tasdiqlangan test dalillari:

- https://github.com/akkiS18/LinePOS/actions/runs/37913718246 — SUCCESS: desktop/core, Windows build, Android build, API26/API35; har emulyatorda **22 ta test**.
- https://github.com/akkiS18/LinePOS/actions/runs/37913718371 — SUCCESS: **97 hisob + 92 component + 155 envelope** fixture C#/Kotlinda mos.
- Receiver testlari: real SQLite/Room, missing dependency, parallel/restart replay, manfiy qoldiq, collision/tamper, o‘nta write-boundary rollback; Androidda >2MiB/1000-item applied receipt restart/relay.
- Bular haqiqiy do‘kon qurilmasi, Wi-Fi orqali end-to-end yoki frontend sinovi bajarilgan degani emas.

## 1. O‘zgarmas biznes qoidalari

- Faqat **xaridorning do‘kondan qarzi**, faqat **UZS**. Pul integer tiyin: 1 so‘m = 100 tiyin.
- Desktop ham, telefon ham yangi nasiya va pul qabul qilishni **LANsiz yakunlaydi**. Keyin LAN orqali birlashadi. Yangi online backend kerak emas.
- Firebase litsenziyalash va CBU kurs olishni o‘zgartirmang. Manfiy ombor qoldig‘i ataylab ruxsat etilgan.
- Qog‘oz daftar alohida yopiladi; dastur noldan boshlaydi. Eski DEBT chekdan customer/account taxmin qilmang. Boshlang‘ich qarz importi bu versiyada yo‘q.
- Savdo: `total = cash + card + newDebt`. Initial cash/card yana debt payment qilib yozilmaydi.
- Har chek: `balance = originalDebt + immutable signed deltas`. Musbat — qarz; manfiy — xaridor krediti.
- Qarzni va kreditni alohida ko‘rsating. Bir chek krediti boshqa chekni avtomatik yopmaydi.
- Offline ikki qurilma bir qarzga pul olsa, ikkala haqiqiy kirim saqlanadi. Excessni yashirish, summani kesish yoki o‘xshash yozuvni taxminan dedup qilish mumkin emas.
- Bir request GUID + bir xil canonical body — bitta amal; shu GUID + boshqa body — conflict.
- Moliyaviy tuzatish append-only. Asl event, allocation, tarixiy narx/kurs yoki chek o‘chirilmaydi/tahrirlanmaydi.
- Return, payment reversal, kredit refund/transfer — **LAN authority** orqali. Offline talabini shu amallarga o‘zboshimchalik bilan kengaytirmang.
- To‘lov collectioni yangi revenue emas. Haqiqiy card fee o‘z sanasidagi xarajat.
- Yangi customer GUIDlar ism/telefon bir xil bo‘lgani uchun avtomatik merge qilinmaydi.

## 2. Qolgan 8 qism va bajarish tartibi

| Tartib | Eski reja IDsi | Natija |
|---|---|---|
| 1 | 3A-2b-2b davomi | Local source envelope freeze va preflight |
| 2 | 3A-2c | LAN transport/capability/push/pull/ACK |
| 3 | 3B | Ko‘p qurilma, restore epoch, kontakt konfliktlari |
| 4 | 4 | Desktop qarz interfeysi va kassa integratsiyasi |
| 5 | 5 | Mobil qarz interfeysi va offline kassa integratsiyasi |
| 6 | 6A | Return/reversal/refund/credit transfer integratsiyasi |
| 7 | 6B | Hisobot, fee, Excel, chek va xaridor ko‘chirmasi |
| 8 | 7 | To‘liq backup/restore, yakuniy regressiya va qo‘lda sinash |

1–3 moliyaviy sinxron poydevori. 4–5 development branchda ishlashi mumkin, ammo 6–8 tugamasdan production/release/merge tayyor deb hisoblamang. Bir sessiyada imkon qadar mazmunli yakunni bajaring; faqat bitta yordamchi yoki hujjat yozilgani uchun butun qismni tugadi deb belgilamang.

## Qism 1 — Jo‘natuvchida o‘zgarmas paketni atomik saqlash

**Maqsad:** local commit bo‘lgan har bir yangi debt amalning aynan o‘sha vaqtdagi to‘liq, keyin yuborish mumkin bo‘lgan paketi mavjud bo‘lsin.

Fayllar ikkala platformada:

- `desktop/PosElectro.Desktop/Debt/` va `app/src/main/java/uz/pos/electro/data/debt/` ichidagi `DebtRepository`, `DebtSyncStore`, `DebtEnvelope`, `DebtEnvelopeInbox`, `DebtSaleReceiver`.
- Kassa yozish yo‘llari: desktop `Data/DatabaseContext.cs`, mobile `data/repository/SaleRepository.kt`. UIga hali ulash shart emas.

Bajarish:

1. Source orchestration API yarating: customer creation, nasiya sale va payment uchun frozen envelope shu biznes tranzaksiyasida saqlansin. Sale API arbitrary callback o‘rniga tekshirilgan immutable snapshot qabul qilsin. Oddiy naqd sotuvning mavjud guardlarini bo‘shatmang.
2. Request, sale, item va stock operation GUIDlari retrydan oldin barqaror bo‘lsin. Item narxi, cost currency, warehouse, FX, fee, timestamp va `StockDelta=-quantity` mahalliy savdo paytida muzlatilsin. Bugungi mahsulot yoki qoldiqdan eski paket yasamang.
3. `SaleFingerprint = SHA256(canonical SaleWire)`. Receiver kutgan movement markerlari va to‘liq receipt lokal manbada ham mos bo‘lsin; sourcega qaytgan echo stock/pulga yana ta’sir qilmasin.
4. `DebtRepository.OpenSale/openSale` hozir writer callbackni event/accountdan **oldin**, `Finish/finish`ni undan keyin bajaradi; replay esa erta return qiladi. Transaction ichidagi participant/hooklarni refactor qiling: event/customer canonical wire, full envelope va receiptni **commitdan oldin**, replayda esa avval saqlangan body bilan ishlating.
5. `DebtSaleReceiver.Prepare` existing sale/eventni rad etadi, `Apply` esa `applying=1` kutadi. Uni sourcega ko‘r-ko‘rona chaqirmang. Umumiy validation/stock planner/persistence qismini ajratish mumkin, lekin receiverning collision va authorization guardlarini olib tashlamang. Source/receiver writer state va journal capture yo‘llari aniq bo‘lsin.
6. Public `ExportEvent/Apply/Receive`ni writer ichidan chaqirib ikkinchi tranzaksiya ochmang. Mavjud internal participantlardan foydalaning. Outer receiptni keyingi mustaqil tranzaksiyada yozish atomiklikni buzadi.
7. CustomerWire optional bo‘lsa ham retryda body o‘zgarmasin: include/omit siyosatini bir marta belgilang. Asl `debt_customer_create:<guid>` snapshotdan foydalaning, tahrirlangan current customer nomidan emas. Customer alohida yaratiladigan amal bo‘lsa o‘z paketi bilan saqlansin; sale bilan bitta amal sifatida yaratilsa shu transactionga kirsin.
8. Payment uchun `TakePayment` allocationini bir marta hisoblang va aynan shu linesni packetga yozing. Keyingi sync/retryda allocation qayta hisoblanmasin. Bir xil request qayta ishga tushganda yangi device epoch bilan paketni qayta yasamang.
9. Saqlashdan oldin codec/HTTP hajmi, item/line limitlari, aniq pul va legacy REAL roundtrip tekshiruvi. Codec-valid son DBda yo‘qotishsiz sig‘masligi mumkin. C#/JVM/Androidda chegaraviy sonlarning qabul/rad etilishi mosligini test qiling; bir platforma yaratgan paket ikkinchisida numeric sabab abadiy qolmasin.
10. Mavjud limitlar: sale max 1000 item; envelope max 6MiB; ichki codec va 8MiB HTTP overheadini koddan tekshiring. Katta bo‘lsa local commitdan oldin rad eting yoki atomik fragmentationni to‘liq loyihalang. Bitta moliyaviy guruhni oddiy batch bo‘laklariga ajratmang.
11. Existing inbox receipt `sync_meta.debt_envelope_v1:<id> = hash + newline + body`. Uni local manba uchun qayta ishlatish/ajratish qarorini hujjatlashtiring; moliyaviy source of truthni ikkita mustaqil outboxda yuritmang. HELD navbat hozircha ochilmaydi.

**Majburiy test:** real local sale/payment/customer commit → export → boshqa DB receive → source echo; restart/retry bir xil bytes; concurrent double submit; original metadata keyin o‘zgarsa eski body o‘zgarmaydi; envelope insert/delete/callback xatosida barcha biznes yozuvlari rollback; katta body/numeric loss local commitdan oldin rad; native Room DEBT o‘qilishi.

**Tugallanganlik:** ikkala platformada haqiqiy source API bor, local amal va yuboriladigan body bir commitda; faqat synthetic receiver fixture bilan “source tayyor” deyilmaydi.

## Qism 2 — LAN transport, capability, push/pull va ACK

Asosiy fayllar:

- Desktop `Services/LocalSyncServer.cs`, `Sync/WifiSyncStore.cs`, `Sync/WifiHttpRequest.cs`.
- Android `data/sync/LocalSyncManager.kt`, `WifiSyncSchema.kt`, `LegacySalePaymentType.kt`.
- `sync/schema.sql` va nusxalari, yangi source/inbox APIlari.

Bajarish:

1. Juftlangan peerning `debtLedgerV1`, store GUID, source actor policy va history epochini aniq tekshiring. UUIDning o‘zi ruxsat emas. Mavjud pairing/token/revocation tekshiruvlarini saqlang.
2. Eski peerga debt sale, stock, customer yoki paymentni oddiy sale/stock paketlari sifatida oqizib yubormang. Oddiy cash sync imkonini qoldirish mumkin, ammo dependency va debt guruhlari ajralishi shart.
3. `pending()`/`freeze()`/`syncOnce()`ni guruhga moslashtiring. Frozen bodydan yuboring, joriy DB qiymatlaridan qayta JSON yig‘mang. Mavjud 250/500 batching limitida butun financial group saqlansin.
4. `acked=-1` hozir HELD. Uni ko‘r-ko‘rona 0 qilish yetmaydi: debt_stock/sale markerlari, dependent metadata, coalescing, legacy bootstrap, full/delta pull ham tekshirilsin. Qarz kutayotganida oddiy narx konflikti mustaqil paymentni to‘xtatmasin.
5. `Applied`/`AlreadyApplied` faqat haqiqiy outermost commitdan so‘ng moliyaviy ACK. `WaitingForDependency` va `WaitingForSaleAdapter` ACK emas. Transport “paket qabul qilindi” holatini final moliyaviy tasdiq bilan aralashtirmang.
6. ACK yo‘qolsa sender aynan eski body/requestni yuboradi; ACKni local belgilash ham atomik. Body mismatchni “already synced” deb yashirmang.
7. Pull uchun snapshot/high-water cursor va dependency tartibini aniqlang. Body apply va cursorning siljishi atomik bo‘lsin. Public Receive o‘z transactionini commit qilishi sabab outer transport cursor boundarysi uchun ichki participant kerak bo‘lishi mumkin; nested independent commit qilmang.
8. Old `/api/sync/download_db` yo‘lini alohida tuzating: u sync_meta’ni tozalashi mumkin, bunda body/seal/dedup dalillari yo‘qoladi. Debt-safe snapshot/restore kontrakti bo‘lmaguncha unsafe yo‘lni himoya qiling.
9. Durable pending retry worker: metadata/dependency kelganda qayta urinish, bounded queue, disk-full/capacity xatolari. Unknown version/kind uchun quarantine/error siyosati; jim tashlash yoki final ACK yo‘q.
10. Ulanish holati va amal holati alohida. “Saqlangan — sinxron kutilmoqda” amali allaqachon haqiqiy local payment. Xato kassirni o‘sha pulni qaytadan kiritishga undamasin.

**Test:** ikki DB + haqiqiy LAN HTTP yo‘li; ACK yo‘qolishi; uzilish/restart; reordered/duplicate/changed packet; old peer; noto‘g‘ri store/actor; 250/500 va hajm chegarasi; full/delta pull; narx konflikti bilan mustaqil collection; cursor oldin yurib ketmasligi; legacy yo‘llardan debt leakage yo‘qligi.

**Tugallanganlik:** to‘liq ikki tomonlama end-to-end saqlash/relay/ACK ishlaydi. Faqat `Receive` chaqiruvi borligi yetarli emas.

## Qism 3 — Bir nechta qurilma, tiklash epochlari va kontaktlar

Fayllar: debt scope/device/repository, LAN host/client state, sync metadata, customer projection; restore yo‘llari bilan kelishilgan kontrakt.

1. Desktop + kamida ikki telefon modeli. Uzilgan har birida final sale/payment; kelish tartibidan qat’i nazar bir xil ledger yig‘indisi.
2. 100 ming qarzga ikkita offline 100 ming payment → haqiqiy kirim 200 ming, qarz 0, kredit 100 ming. Hech qaysi eventni o‘chirmang yoki qayta taqsimlamang.
3. Bir snapshotdan ikki qurilma boshlanganida yangi writer/device epochlar boshqa bo‘lsin. Tarixiy GUIDlar o‘zgarmasin; yangi device sequence collision bo‘lmasin. Mavjud repository per-instance epochini hisobga oling.
4. Desktop eski backupga qaytganda server history epoch/cursor rollback aniqlansin. Eski cursor bilan “hammasi synced” deyilmasin. Full reconciliation local pendingni yo‘qotmasin.
5. Store binding va server restore epochni alohida tushuncha qiling. Boshqa do‘konga ledgerni avtomatik qo‘shmang.
6. Bir xil ism/telefon bilan mustaqil yaratilgan customers alohida GUID bo‘lib qoladi. Kontakt tahriri uchun revision/conflict siyosati; archive qarzni o‘chirmaydi, oldin qabul qilingan offline amalni yaroqsiz qilmaydi.
7. Moliyaviy tarix immutable; kontakt konfliktini hal qilish paymentlardan mustaqil.

**Test:** uch qurilma, offline excess, turli delivery tartiblari, duplicate relay, bir backupdan ikki nusxa, server eski backup, customer rename/archive/duplicate contact. Har testda debt, credit, cash/card, event count va stockni alohida solishtiring.

**Tugallanganlik:** convergence va recovery kontrakti test bilan isbotlangan; boshlang‘ich component convergence testi bu qismning o‘rnini bosmaydi.

## Qism 4 — Desktop frontend va kassa integratsiyasi

Fayllar: `MainWindow.xaml`, tegishli MainViewModel/navigatsiya, `ViewModels/CashierViewModel.cs`, `Views/CashierView.xaml`, yangi Qarzlar View/ViewModel va debt application service. Mavjud WPF dizayn va navigatsiyaga mos qiling.

1. Menyu: **Kassa / Ombor / Qarzlar / Hisobotlar / Wi-Fi Sinxron**. Tab almashishi, keyboard/focus va boshqa tablarning ochilishini tekshiring.
2. Qarzlar: jami musbat qarz, qarzdorlar, overdue; kredit alohida. Ism/telefon qidirish, customer yaratish. Filtrlar: Qarzi bor, Muddati o‘tgan, Yopilgan, Ortiqcha to‘lov, Barchasi.
3. Customer: ism majburiy, telefon/izoh optional. Cheklar va immutable tarix; sale/payment/return raqami, summa, qoldiq, qurilma va sync holati. Arxiv qarzni o‘chirmaydi.
4. Kassa Nasiya oynasi: customer, oldingi qarz, total, cash/card, readonly qarz, optional due date/izoh. To‘liq nasiya default 0/0. BRAK qarz yaratmaydi; oddiy savdo customer talab qilmaydi.
5. Hold savat har biri customer/due date holatini alohida saqlaydi. Customer kartasidan kassaga o‘tish mavjud savatni jim tozalamaydi.
6. To‘lov olish: cash/card/aralash, amount, oldest-first yoki tanlangan chek, allocation va qoldiq preview. Ma’lum qarzdan ortiq yangi input blok; keyinchalik syncdan kelgan haqiqiy excess saqlanadi.
7. Submit disable + durable request GUID/retry; success faqat local commitdan keyin. Printer/share xatosi yangi payment yaratmasin. Network holatini payment muvaffaqiyati bilan bog‘lamang.
8. Due date timezone-aware mahalliy sana: muddat kunining oxiridan keyin overdue. Due date yo‘q bo‘lsa overdue emas.
9. UI moliyaviy SQL yozmasin; 1-qismdagi tekshirilgan API ishlasin. Authorization backend/service qatlamida; faqat tugmani yashirish yetmaydi.

**Test:** offline full/partial nasiya, payment, double click, restart keyin retry, hold savatlar, qidiruv/filtr, keyboard, tablar, printer xatosi. Windows build va real oynalarni qo‘lda tekshirish dalili alohida.

**Tugallanganlik:** desktopdagi asosiy offline jarayon haqiqiy DB bilan ishlaydi; UI mocklar bilan tayyor deb belgilanmaydi.

## Qism 5 — Mobil frontend va offline kassa integratsiyasi

Fayllar: `MainScreen.kt`, `ui/cashier/CashierViewModel.kt`, `CashierScreen.kt`, `CheckoutPaymentDialog.kt`, `ReceiptDialog.kt`; yangi debt ekran/ViewModel/service; `data/repository/SaleRepository.kt`.

1. Pastki menyu: **Kassa / Ombor / Qarzlar / Hisobotlar / Sozlamalar**. Index/title/back handler va saved state mosligi.
2. Customer kartalari → detail screen. Desktop bilan ayni KPI, qidiruv, filter, debt/credit va tarix qoidalari.
3. Payment oynasida Nasiya uchun 2x2 tanlov, customer qidirish/inline qo‘shish, cash/card/qarz/due date. Oddiy `completeSale`ning DEBTni rad etishini shunchaki o‘chirib qo‘ymang: alohida atomik source APIga ulang.
4. Telefon desktopdan uzilganida sale/payment yakunlanadi. Internet/license/CBU kodini shu feature uchun qayta yozmang.
5. Process death, rotation/navigation va qayta bosishda request identity saqlansin. UI pending va server confirmationni farqlasin; local saved paymentni draftga qaytarmang.
6. 360dp, katta shrift, keyboard, scrolling, back, 5 touch target, accessibility. Warehouse va hisob sahifalariga navigatsiya regressiyasi bo‘lmasin.
7. Kredit refund/transfer va return tuzatishlarini LANsiz bajarilgandek ko‘rsatmang. UI qismi keyingi authority API bilan ulanadi.

**Test:** API26/35 Room + ViewModel/jarayon testlari; imkon bo‘lsa haqiqiy telefon. Ikki marta submit, rotation/restart, no-Wi-Fi savdo/payment, reconnect status va desktopdagi ayni GUIDli chek. Xuddi desktopdagidek allocation va pul invariantlari.

**Tugallanganlik:** ikkala frontend ayni biznes kontraktidan foydalanadi; faqat ekran ko‘rinishi yetmaydi.

## Qism 6 — Return, reversal, refund va kredit transferi

Fayllar: desktop `Returns/ReturnStore.cs`, `ReturnReversal.cs`, `Models/ReturnAccounting.cs`, debt core/repository/wire/envelope; Android `ui/reports/ReturnDialog.kt`, LAN client/drafts va debt UI.

Muhim: pure accountingda bu amallar mavjud bo‘lishi DB/wire/transport implementation borligini anglatmaydi. Hozirgi v1 component codec asosan customer, sale_open, paymentni qo‘llaydi. Yangi eventlarni qo‘shishda schema/version/capability/backward compatibilityni aniq yangilang; unknown kindni ko‘r-ko‘rona o‘tkazmang.

1. Bitta LAN authority, permission va actor audit. Request GUID + hash + durable natija. Target reversal bir marta, boshqa body reject.
2. Return quote: `returnedValue = debtOffset + cashRefund + cardRefund`. Offset original sale accountdan; boshqa customer/chekka o‘tmaydi.
3. Quote va commit oralig‘ida payment kelsa writer transactionida qayta tekshiring; pul taqsimoti o‘zgarsa kassir yangi quote tasdiqlaydi.
4. RT, sale/item/stock, debt event, refund va receipt bitta commit. Yaxshi/brak qaytarilgan tovarning mavjud stock/cost qoidalari saqlanadi.
5. Return reversal asl offset/cash/card taqsimotini teskari qiladi. Bugungi balance asosida asl refundni qayta hisoblamang.
6. Payment reversal xato yozuvni tuzatadi; real pul refundidan alohida UI/semantika. Fee reversal faqat haqiqatan qaytgan komissiya miqdorida.
7. Credit refund kreditni kamaytiradi va cash/card chiqimini yozadi; principalni yangi revenue/expense deb qo‘shmaydi. Credit transfer source kredit va target qarzni atomik kamaytiradi, cash/revenue 0. Ownership va amountni tekshiring.
8. Returndan keyin kelgan eski offline paymentni rad qilmang; zarur bo‘lsa credit hosil bo‘ladi. Eski returnni mutatsiya qilmang.

**Test:** D13–D16. 100k sale/20k paid/80k debt: 30k return → debt50k/refund0; 90k return → debt0/refund10k. Quote race, late payment, ikki qurilma reversal, double refund/transfer, authorization, trigger failure, exact replay.

**Tugallanganlik:** barcha yangi eventlar DB, codec, transport, UI va rollback bo‘yicha to‘liq; lokal fake success yo‘q.

## Qism 7 — Hisobotlar, foyda, Excel va chek/ko‘chirma

Fayllar: ikkala platformadagi `SaleAccounting`, Reports View/ViewModel/Screen; receipt va mavjud Excel export helperlari; yangi debt/cashflow projectionlar.

1. Savdo sanasida to‘liq revenue/cost/profit bir marta. Keyingi collection kuni cashflow va haqiqiy fee; sale timestampi o‘zgarmaydi.
2. Cash/card/kredit/qarz alohida. Customer krediti do‘konning boshqa qarzlarini kamaytirib ko‘rsatmasin.
3. Davr `[start inclusive, end exclusive)`, do‘kon timezone. Closing balance uchun oldingi barcha tegishli eventlar kerak: `opening + periodChanges = closing`.
4. Cached payment FX snapshot saqlanadi; keyingi USD fee/profit hisobida bugungi kurs ishlatilmaydi. Kurs yo‘q bo‘lsa USD unknown, soxta 0 yoki bugungi kurs emas. CBU xizmatining o‘zini o‘zgartirmang.
5. Category/warehouse sales itemlarga qo‘llanadi. Collection uchun bu filtrlar birinchi versiyada disabled/tushuntirilgan; umumiy collectionni filtrlangan deb ko‘rsatmang. BRAK va return statistikalari ajralgan bo‘lsin.
6. Chek: initial paid, original debt, later payments, returns, current balance/status va credit. Stable LP/RT/RV GUID identifikatorlari; local auto-increment IDga qaytmang.
7. UI, Excel va statement bitta hisoblash projectionidan foydalansin. Tarixiy nom/cost/rate saqlansin. Paymentni soxta sale receipt qilib kiritmang.
8. Customer statement/share faqat foydalanuvchi tashabbusi bilan. Printer/share retry faqat chiqarishni takrorlaydi, pul amalini emas.

**Test:** D17–D20 va D26. Nasiya bugun/collection ertaga; oldingi davr qarzi; midnight/timezone; kech kelgan event; rename/FX o‘zgarishi; category/warehouse/BRАK/return; UI va Excel jami teng; fee ikki marta ayrilmasligi.

**Tugallanganlik:** UZS/USD profit va cashflow ataylab farqlanadigan misollar bilan isbotlangan, eski oddiy savdo hisobotlari buzilmagan.

## Qism 8 — Backup/restore, regressiya va haqiqiy sinov

Fayllar: `AppDatabase.kt`, `DatabaseBackupExporter.kt`, `data/backup/TelegramBackupService.kt`, `DatabaseAutoBackupWorker.kt`, desktop `DatabaseContext.BackupDatabase`, LAN download/restore yo‘llari.

1. Snapshot bir transaction holatida customers/accounts/events/lines/command receipts/inbox/journal/scope/epoch va **sync_meta body/seallarini** qamrasin. Faqat sales borligini backup kerakligiga shart qilmang.
2. Faqat payment bo‘lgan kun, unsent local event, missing-dependency inbox va >2MiB applied body bilan restore testi.
3. Restore oldidan recovery snapshot, schema/integrity/FK tekshiruvi, safe staged replacement. Muammo bo‘lsa avvalgi ishlaydigan baza saqlansin.
4. Yangi device epoch, server history/cursor reconciliation 3-qism bilan bir xil siyosat. Tiklangan nusxa eski amallarni yana pul/stock deb qo‘shmasin.
5. Telegram jo‘natilishi internet talab qiladi; offline bo‘lsa local backup qoladi va haqiqiy yetkazilish tasdiqlanmaguncha “Telegramga yuborildi” deyilmaydi. Bot token/chat IDni kod, log yoki qo‘llanmaga oshkor qilmang.
6. Amaldagi Room14/debt schema1 bazani ham, oldingi supported versiyalarni ham sinang; schema o‘zgarsa safe migration va asset nusxalarini yangilang. Destructive migration qo‘shmang.
7. D01–D27 bo‘yicha yakuniy coverage jadvali: test nomi, avtomatik/manual, natija, dalil. Unit testni LAN yoki printer sinovi deb belgilamang.
8. Haqiqiy Windows + Android sinovi uchun guide: backup olish → alohida test baza → pairing → offline sale/payment → reconnect → duplicate/restart → return race → report/export → backup/restore. Expected summalar va qayta tiklash yo‘riqnomasi bilan.
9. Main/release oldidan to‘liq CI va fizik sinov natijalari ko‘rib chiqiladi. Ushbu handoffning o‘zi merge yoki production deploy uchun yangi ruxsat emas. Release bo‘lsa AGENT.md’dagi PROD qoidalariga amal qiling.

**Tugallanganlik:** yangi qarz moduli bilan eski oddiy sale, manfiy stock, BRAK, return, Wi-Fi va backup regressiyalari o‘tgan; bajarilmagan physical testlar yashirilmagan.

## 3. Testlarni qanday ishlatish va dalilni saqlash

Repo ildizida mavjud buyruqlar:

```bash
python -m unittest discover -s tests -p 'test_*.py' -v
dotnet run --project tests/WifiSync.CoreTests
dotnet run --project tests/Business.CoreTests
dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj --configuration Debug
bash gradlew :app:assembleDebug :app:assembleDebugAndroidTest --no-daemon --stacktrace
bash gradlew :app:connectedDebugAndroidTest --no-daemon --stacktrace
git diff --check
```

Windows Gradle uchun `gradlew.bat`; WPF build Windows runnerda. Connected testlar haqiqiy/emulyator qurilma talab qiladi; API26 va API35 ikkalasi kerak. Mahalliy SDK yo‘q bo‘lsa “test o‘tdi” demang: `.github/workflows/wifi-sync.yml` va `debt-core.yml` orqali tekshiring. Pure parityning aniq Kotlin compiler/JSON dependency/buyruqlari `debt-core.yml`da; qo‘lda taxminiy boshqa dependency bilan almashtirmang.

Mavjud muhim testlar:

- `tests/Business.CoreTests/DebtRepositoryTests.cs`, `DebtSyncStoreTests.cs`, `DebtEnvelopeInboxTests.cs`, `DebtSaleReceiverTests.cs`.
- Android `app/src/androidTest/java/uz/pos/electro/data/business/` ichidagi debt testlari, `DebtSaleReceiverTest.kt`, `DebtPaymentTypeTest.kt`.
- `tests/debt/` umumiy fixtures/runners; production va Android asset nusxalarining driftini mavjud test tekshiradi.
- `tests/WifiSync.CoreTests` va mavjud business/return/backup testlarini saqlang.

Har mazmunli qismdan keyin:

1. Diff va whitespace ko‘rigi; o‘zgargan logikaga meaningful test. Har kichik kosmetik editga alohida test yozish shart emas.
2. Feature branchga aniq commit/push. CI tekshirgan SHA aynan runtime o‘zgarishining SHA si bo‘lsin.
3. Barcha kerakli joblar tugashini tekshiring; build success runtime test success emas. Fail bo‘lsa sababini tuzating, yangi SHA ni qayta tekshiring.
4. `DEBT_CHECKPOINT_UZ.md`ni yangilang: tugagan narsa, qolgan narsa, commit, CI URL/natija, real cheklov, aniq keyingi vazifa. Reja jadvali va API kontrakti ham mos bo‘lsin.
5. Docs-only yakuniy checkpoint runtime qayta testini talab qilmaydi, agar kod o‘zgarmagan bo‘lsa. Ish daraxtini tekshiring; boshqa agent/foydalanuvchi o‘zgarishini commitga aralashtirmang.

Branchdagi GitHub Actions push filtrlari `feature/customer-debt`ga bog‘langan. Boshqa ish branchi tanlansa CI o‘zi ishga tushdi deb taxmin qilmang; amaldagi workflow trigger yoki workflow_dispatch orqali tekshiring.

## 4. Agent ishni chala qoldirmasligi uchun tartib

- Avval navbatdagi qismning hajmi va eng xavfli invariantini qisqa ayting, so‘ng ishni bajaring. Har mayda qadam uchun tasdiq so‘ramang; doira avval tasdiqlangan.
- Foydalanuvchi limitini aniq o‘lchay olmaysiz. “Limit aniq yetadi” demang; bounded yakun tanlang. Bir sessiyada bir necha bog‘liq bo‘lak sig‘sa birlashtiring.
- CI tugashini kutish vaqtida yangi katta feature ochmang; kontrakt/checkpointni tayyorlash mumkin.
- Sinovdan o‘tmagan kodni tayyor deb e’lon qilmang. To‘xtash zarur bo‘lsa exact dirty files/commit/CI run va keyingi amalni yozing. User o‘zgarishlarini yo‘qotmang.
- Main/release/production alohida yakuniy qadam. Debt UI ochilgani productionga tayyorlik emas.
- Asossiz yangi arxitektura, cloud server, qo‘shimcha paket yoki umumiy katta refactor qo‘shmang. Mavjud kontraktlarni saqlab, zarur transaction/transport refactorini test bilan qiling.
- Agent parallel ishlatsa shared file/branch ownershipni aniq ajratsin; bir necha agent bir transaction faylini nazoratsiz o‘zgartirmasin. Parallel agent talab qilinmaydi.

## 5. Yangi agentga beriladigan tayyor prompt

Quyidagini yangi coding agentga yuboring:

```text
LinePOS loyihasidagi qarz daftarini avvalgi ish nuqtasidan davom ettir.
Repo: https://github.com/akkiS18/LinePOS
Branch: feature/customer-debt

Avval git status va remote HEADni tekshir, mening lokal o‘zgarishlarimni yo‘qotma.
AGENT.md, docs/DEBT_CHECKPOINT_UZ.md va docs/DEBT_AGENT_HANDOFF_UZ.md ni o‘qi.
Handoffda qolgan 8 qism, aniq fayllar, biznes qoidalari va acceptance testlar bor.
Eng so‘nggi checkpointga qarab birinchi tugamagan qismdan boshlagin.
Handoff yozilgan paytda navbatdagi ish: ikkala platformada local source envelope
freeze/preflight; desktop va Android receiverlar allaqachon tayyor va testdan o‘tgan.

Ishni noldan yozma. Yangi nasiya va qarz to‘lovi ikkala qurilmada offline yakunlanadi,
keyin faqat LAN orqali sinxronlashadi. Qarz UZSda. Firebase/CBUga tegma,
manfiy stock ruxsatini saqla. Qog‘oz daftarni import qilma.
Return/reversal/refund/credit transfer LAN authority orqali qoladi.
Pul va stockni ikki marta yozish, partial commit/ACK, historical body mutatsiyasi,
collectionni yangi revenue deb hisoblash mumkin emas.

Imkon qadar kattaroq, ammo test bilan yakunlanadigan ish qismini bajar.
Har kichik qadamda ruxsat so‘rama. Natijani real SQLite/Room va zarur CI bilan tekshir,
feature branchga commit/push qil, checkpointda SHA/test dalili/keyingi vazifani yoz.
SDK yoki qurilma yetishmasa testni bajardim deb aytma; CI yoki mavjud test imkonidan foydalan.
Barcha integratsiya/release gates tugamaguncha main'ga merge yoki production release qilma.
Menga o‘zbekcha qisqa progress va oxirida nima tugagani/nima qolgani haqida aniq hisobot ber.
```

Bu prompt boshqa agentning natijasini kafolatlamaydi; qo‘llanmadagi testlar va yakun mezonlari uning ishini tekshirish uchun berilgan.

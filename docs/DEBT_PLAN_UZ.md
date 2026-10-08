# Qarz daftari — texnik va frontend reja

Holat: 1-bosqich reja, 2A hisoblash yadrosi va 2B-1 sxema/migratsiya va 2B-2 lokal repository hamda 3A-1 wire codec/validator va 3A-2a DB component export/atomic receiver yakunlangan. 3A-2b-1 full-envelope codec/validator yakunlandi va CI testlari o‘tdi; 3A-2b-2a durable inbox/customer-payment receiver yakunlandi va CI testlari o‘tdi; concrete sale/stock va local freeze hali yo‘q. Ilovaga integratsiya hali amalga oshirilmagan; joriy holat DEBT_CHECKPOINT_UZ.md da.
Sana: 2026-10-05 (Asia/Tashkent).
Tekshirilgan asos: main `b631a27241a5eb25501a6e4ab316c4aebdae4460`.
Ish branchi: `feature/customer-debt`.

## 1. Tasdiqlangan doira

- Faqat xaridorning do‘kondan qarzi; faqat UZS.
- Desktop va telefon yangi nasiya savdosini hamda qarz to‘lovini bir-biriga ulanmasdan mahalliy bazada yakunlaydi. Keyin LAN orqali sinxronlashadi. Internet serveri kerak emas.
- Mahalliy saqlangan to‘lov haqiqiy amalga oshirilgan amal: uni sinxron tasdig‘igacha oddiy draft deb hisoblamaymiz.
- Qog‘oz daftarni alohida tugatish, dastur daftarini noldan boshlash — hozirgi reja. Import va boshlang‘ich qarz kiritish birinchi versiyada yo‘q. Eski chekdan taxminiy qarz yaratmaslik.
- Firebase litsenziyalash, CBU kurs olish va ataylab ruxsat etilgan manfiy ombor qoldig‘i o‘zgarmaydi.
- Yetkazib beruvchilar, USD qarz, foiz/penya, avtomatik Telegram xabarlari va ixtiyoriy avans yig‘ish doiraga kirmaydi.
- Offline talabi yangi nasiya savdosi va pul qabul qilishga tegishli. Mahsulot qaytarishning mavjud LAN orqali tasdiqlash qoidasi saqlanadi. To‘lovni bekor qilish, kreditni boshqa chekga ko‘chirish va ortiqcha pulni qaytarish ham LAN orqali yagona tasdiqlashdan o‘tadi.
- Bir do‘konning juftlangan qurilmalari. Birinchi ulanishdan oldin yaratilgan mahalliy qarzlar boshqa do‘konga avtomatik aralashtirilmaydi; birinchi biriktirish ma’lumotli tasdiq bilan, keyingi do‘kon almashtirish bloklangan sinxron va alohida ko‘chirish tartibi bilan.

## 2. Tekshirilgan kod va ulanish nuqtalari

Quyidagilar GitHub’dagi yuqoridagi commitdan o‘qildi. AGENT.md tarixiy logidagi eski versiyalar o‘rniga amaldagi kod asos qilib olindi.

| Qism | Hozirgi holat | Kerakli o‘zgarish |
|---|---|---|
| `app/src/main/java/uz/pos/electro/data/repository/SaleRepository.kt` | `completeSale` Room tranzaksiyasida savdo/ombor yozadi; BRAKdan tashqari cash + card = total talab qiladi | Nasiya uchun customer GUID, qarz summasi va atomik ledger yozuvini kiritish; oddiy savdolar tekshiruvini saqlash |
| `desktop/PosElectro.Desktop/Data/DatabaseContext.cs` | `InsertSale`, SQLite migratsiyalar, hisobotga chek o‘qish | Savdo va qarzning bitta tranzaksiyasi; qarzga xos yangi xizmat; eski chek fallbacklarini nasiya bilan tekshirish |
| `app/src/main/java/uz/pos/electro/data/local/AppDatabase.kt` | Room versiyasi 13 | Yangi bo‘sh jadvallar bilan keyingi migratsiya; implementatsiya oldidan versiyani qayta tekshirish |
| `sync/schema.sql` | Journal, group, versions va return jadvallari | Ikkala platformaga bir xil qarz sxemasi; uch schema nusxasi mosligi |
| `desktop/PosElectro.Desktop/Sync/WifiSyncStore.cs` | Push bir tranzaksiya; op ID/payload tekshiruvi; faqat mavjud kindlar qabul qilinadi | Customer va debt eventlar uchun aniq validator, idempotency va pull |
| `app/src/main/java/uz/pos/electro/data/sync/LocalSyncManager.kt` | `freeze` 250 amal tanlaydi, guruhni to‘liq qo‘shadi, 500 limit; body qayta yuborishda o‘zgarmaydi | Yangi dependency tartibi, qarz guruhlari, capability, inbox/outbox va holat |
| `desktop/PosElectro.Desktop/Returns/ReturnStore.cs` | Qaytarish qiymati cash/card refund bilan bog‘langan | Tovar qaytarish qiymati = qarzdan ayirish + real pul qaytarish; RT/RV o‘zgarmas tarix |
| `desktop/PosElectro.Desktop/Models/SaleAccounting.cs` va mobil analogi | Daromad − tannarx − kartaga tegishli xarajat; tarixiy kurs | Qarz undirishni daromadga qo‘shmaslik; keyingi to‘lov komissiyasini alohida davr xarajati qilish |
| `app/src/main/java/uz/pos/electro/util/DatabaseBackupExporter.kt` | Barcha jadval/sxemalarni snapshotga nusxalaydi, staged restore qiladi | Yangi jadvallar/outbox saqlanishi va restore identifikatorlarini tekshirish |
| `desktop/PosElectro.Desktop/MainWindow.xaml` | Yuqori gorizontal menyu | Ombordan keyin Qarzlar |
| `app/src/main/java/uz/pos/electro/MainScreen.kt` | Pastda 4 ikonali tab, indeksli navigatsiya | 5 tab, Qarzlar uchun alohida ekran; indeks/back handler/title mosligi |

`DEBT` enum qiymatining o‘zi qarz funksiyasi mavjudligini anglatmaydi. Eski DEBT yozuvlarini yangi mijoz hisobiga taxminan bog‘lash taqiqlanadi; uchrasa tekshiruv ro‘yxatida saqlanadi.

## 3. Pul va hisob qoidalari

Yangi qarz modulida pul INTEGER minor unit bilan: 1 so‘m = 100 tiyin. UI odatda butun so‘m ko‘rsatadi, kasr bo‘lsa 2 xona. Kotlin Long/BigDecimal va C# long/decimal; JSONda minor-unit butun son, Double orqali qayta hisoblash yo‘q. Mavjud savdo qiymati ikki kasrga yaxlitlangandan keyin bir marta o‘tkaziladi. Manfiy input, NaN, infinity, overflow va 2 xonadan ortiq foydalanuvchi kasri rad etiladi. Yig‘indilar ham checked arifmetika bilan.

Savdo uchun: `jami = dastlabki_naqd + dastlabki_karta + yangi_qarz`.
Nasiya qarzi > 0 bo‘lsa xaridor majburiy. Dastlabki naqd/karta keyingi qarz to‘lovi sifatida takror yozilmaydi. BRAK mijoz qarzi yaratmaydi.

Har bir chek hisobi: `qoldiq = boshlang‘ich_qarz + barcha_imzolangan_qarz_o‘zgarishlari`.
Musbat qoldiq — xaridor qarzi; manfiy qoldiq — xaridor foydasiga ortiqcha to‘lov.
Xaridor sahifasi qarzdor cheklar summasi va ortiqcha to‘lovni alohida ko‘rsatadi. Boshqa chek qarzini ortiqcha to‘lov bilan yashirin avtomatik yopish yo‘q; maxsus transfer amali kerak.
Do‘kon KPI: barcha musbat qoldiqlar yig‘indisi va barcha kreditlar yig‘indisi alohida; bir xaridor krediti boshqa xaridor qarzini kamaytirmaydi.

| Amal | Qarzga ta’sir | Kassa/pulga ta’sir | Savdo/foydaga ta’sir |
|---|---|---|---|
| Nasiya savdo | Qolgan summa ortadi | Faqat dastlab olingan cash/card | To‘liq savdo va tannarx bir marta |
| Qarz to‘lovi | Taqsimlangan summa kamayadi | Olingan cash/card | Yangi daromad yo‘q; haqiqiy karta komissiyasi alohida xarajat |
| Mahsulot qaytarish | Shu chek qarzidan ajratilgan qism kamayadi | Faqat real refund | To‘liq qaytarilgan qiymatga revenue reversal, mavjud tannarx/brak qoidasi |
| To‘lovni bekor qilish | Asl to‘lovning qarz effekti tiklanadi | O‘sha pul kirimining teskari yozuvi | Savdo o‘zgarmaydi; fee reversal faqat haqiqiy qaytgan miqdor |
| Ortiqcha to‘lovni berish | Kredit kamayadi | Cash/card chiqimi | Yangi savdo yoki xarajat sifatida qarz asosiy summasini hisoblamaslik |
| Kreditni boshqa chekga qo‘llash | Manba kredit kamayadi, maqsad qarz kamayadi | 0 | 0 |

To‘lov bekor qilish xato yozuvni tuzatish uchun; real olingan pulni qaytarish bilan bir xil UI emas. Real pul olinmagan bo‘lsa bekor qilish sababida ko‘rsatiladi. Barcha tuzatishlar append-only; asl satr o‘chmaydi.

Misollar:
- 1 000 000 savdo, cash 200 000, card 300 000 → qarz 500 000; oldingi qarz 150 000 bo‘lsa jami 650 000.
- 100 000 qarzga ikki uzilgan qurilma 100 000 dan oladi → qarz 0, ortiqcha 100 000, real kirim 200 000. Ikkala amal saqlanadi.
- 100 000 savdo, dastlab 20 000 to‘langan, 80 000 qarz. 30 000 qaytarish → qarz 50 000, pul refund 0.
- Xuddi shu chekdan 90 000 qaytarish → qarzdan 80 000, pul refund 10 000. Keyin uzilgan qurilmadan eski qarzga to‘lov kelsa, kredit hosil bo‘ladi; oldingi qaytarish qayta yozilmaydi.

## 4. Rejalashtirilgan ma’lumot modeli

Bu jadval nomlari implementatsiya uchun kontrakt; hozir SQL migratsiya yaratilmagan.

| Jadval | Asosiy maydonlar va cheklovlar |
|---|---|
| `debt_customers` | GUID PK, ism, normalized phone (unique emas), note, archived, revision, yaratish vaqti/qurilmasi; ism bo‘sh emas |
| `debt_accounts` | GUID PK, sale_guid UNIQUE, customer_guid FK, original_debt_minor, due_date (mahalliy sana yoki null), tarixiy xaridor nomi; original summa o‘zgarmaydi |
| `debt_events` | GUID PK, request_guid UNIQUE, schema_version, kind, customer GUID, actor/device/store GUID, occurred_at UTC, device sequence, immutable payload + canonical hash, cash/card/fee minor, fee kursi snapshot, reference GUID |
| `debt_event_lines` | event_guid + line_index PK, account GUID FK, signed debt_delta_minor; bir amaldagi cheklar bo‘yicha taqsimot |
| `debt_command_receipts` | Yagona authority tasdiqlaydigan refund/reversal/transfer request GUID + hash + natija; unique original reversal target |
| `debt_sync_inbox` | To‘liq qabul qilinmagan/dependency yetishmagan paketlar va xato; ledgerga qo‘shilmaguncha tasdiq yo‘q |

Outbox uchun mavjud `sync_journal` ishlatiladi; yangi alohida savdo outboxini parallel yuritmaslik. Qoldiq/status/overdue qayta hisoblanadigan projection; source of truth emas. Kerakli indekslar: customer+date, sale GUID, event reference, outbox ack/state. Foreign keylar har ikkala platformada haqiqatan yoqilganini tekshirish.

Offline to‘lov paytida ma’lum ochiq cheklarga eng eskisidan taqsimot saqlanadi: `sale.created_at UTC, sale.guid` tartibi. Kassir muayyan chekni tanlashi mumkin. Saqlangan taqsimot keyin qayta avtomatik taqsimlanmaydi. Parallel amallar natijasida bir chek ortiqcha to‘langan bo‘lsa kredit sifatida ko‘rinadi. Qurilma vaqti noto‘g‘ri bo‘lsa tarix tartibi/eng eski tanlashga ta’siri bor, lekin pul yig‘indisi qabul qilish tartibiga bog‘liq emas.

Bir xil telefon yoki ismli ikki offline mijoz avtomatik birlashtirilmaydi. Ogohlantirish va operator tekshiruvi; birlashtirish vositasi keyingi kengaytirish bo‘lishi mumkin. Kontakt tahriridagi konflikt qarz amallarini to‘xtatmasin: mavjud GUID ostidagi moliyaviy amallar davom etadi, yangi mijoz create dependency atomik yuboriladi. Arxivlash qarzni o‘chirmaydi va oldin offline yaratilgan valid amalni rad etmaydi.

## 5. Offline va sinxron protokoli

1. Tasdiqlashdan oldin request GUID yaratiladi va qayta bosish/qayta ishga tushirishda aynan shu amal uchun saqlanadi.
2. Mahalliy tranzaksiya savdo + item + stock + qarz + outboxni birga yozadi. To‘lovda event + lines + outbox birga. UI muvaffaqiyat faqat commitdan keyin.
3. Mavjud LAN transportiga `debtLedgerV1` capability qo‘shiladi. Eski qarz ma’lumotini tushunmaydigan peerga nasiya savdosini oddiy sale sifatida yuborish taqiqlanadi. UI yangilash zarurligini ko‘rsatadi; mahalliy amallar saqlanadi.
4. Moliyaviy eventlar immutable. GUID + canonical payload/hash bir xil bo‘lsa replay no-op; bir GUID boshqa body bilan kelsa ko‘rinadigan integrity xatosi. Server op IDdan tashqari event/request GUIDni ham dedup qiladi.
5. Butun guruh: yangi customer → sale/items → debt account/event → stock. Guruh uzilgan yoki invalid bo‘lsa qisman ledger yo‘q. 500 limitdan katta moliyaviy guruh mahalliy commitdan oldin aniqlanadi yoki protokolda atomik envelope qo‘llanadi; navbatda abadiy qoldirish qabul qilinmaydi.
6. ACK faqat durable commitdan keyin; telefon accepted GUIDlarni alohida tranzaksiyada belgilaydi. ACK yo‘qolsa frozen body/request bilan qayta yuboradi.
7. Pull snapshot/high-water cursor bilan customer/accounts/eventsni dependency tartibida birga beradi. Qabul qiluvchi saqlash va cursorni bir tranzaksiyada siljitadi; source devicega qaytgan event yana pulga ta’sir qilmaydi.
8. Desktop umumiy ledgerning almashish markazi; yangi sale/paymentga oldindan ruxsat beruvchi server emas. Snapshot lokal pending eventni o‘chirmaydi. Narx konflikti mustaqil qarz to‘lovlarini navbatda cheksiz ushlab qolmasligi uchun guruh bo‘yicha ajratish kerak.
9. To‘lov/reversal/refundning bog‘liqligi yetishmasa qayta so‘rash yoki inboxda saqlash; jim tashlash yoki yolg‘on ACK yo‘q. Unknown schema/kind versiya xatosi bilan saqlanadi.
10. Qarzni ortiqcha undirishni uzilgan qurilmalarda mutlaq oldini olib bo‘lmaydi. Kelgan valid pul yozuvlari birlashtiriladi, kredit ko‘rsatiladi. Operator real ikki kirimni tasdiqlaydi yoki xato dublikatni nazoratli reversal bilan tuzatadi. O‘xshash summa/sana asosida avtomatik o‘chirish yo‘q.

Payment local states: `Saqlangan — sinxron kutilmoqda`, `Sinxronlangan`, `Sinxron xatosi — amal saqlangan`. Sinxron xatosi kassirni ayni pulni yangidan olishga undamasin; bir xil request bilan qayta uzatish ishlaydi. Transport ulanish holati va moliyaviy amal holati alohida.

## 6. Qaytarish, tuzatish va hisob yaxlitligi

- Qarzli chek uchun qaytarish quoteida qaytarilgan tovar qiymati, debt offset, cash/card refund, fee reversal alohida. `returned_value = debt_offset + cash_refund + card_refund`.
- Quote va commit orasida to‘lov kelganda desktop tranzaksiyada qayta tekshiradi; o‘zgargan taqsimotni kassir qayta ko‘radi. Yashirincha boshqa pul summasi tasdiqlanmaydi.
- Debt offset shu original sale accountiga tegishli; boshqa chekga avtomatik o‘tmaydi. Qaytarishdan keyin kechikkan offline payment kredit bo‘lishi mumkin.
- Qaytarishni bekor qilish o‘sha RTning aniq qarz va cash/card taqsimotini teskari qiladi; yangi qoldiqdan qayta taxmin qilmaydi. RT/RV va debt event bir atomik commit.
- Pulni qaytarish, payment reversal va kredit transferi faqat joriy authority orqali; har target bir marta bekor qilinadi. Mobile LANsiz bu amallar uchun draft ko‘rsatadi. Offline sale/payment o‘z kuchida qoladi.
- Operator identifikatori va qurilma auditda majburiy. Mavjud operator/rol tizimi implementatsiyada tekshiriladi; rol yo‘q bo‘lsa UI tugmasini yashirishni authorization deb hisoblamaslik, himoyalangan tuzatish gate qo‘shish. Firebase licensing kodi bilan aralashtirmaslik.

## 7. Frontend kontrakti

Desktop yuqori menyu: Kassa / Ombor / Qarzlar / Hisobotlar / Wi-Fi Sinxron.
Mobil pastki menyu: Kassa / Ombor / Qarzlar / Hisobotlar / Sozlamalar; 5 ta touch target, tanlangan tab nomi va accessibility description. Kichik ekran, katta shrift, klaviatura ochilishi bilan tekshirish.

Qarzlar bosh sahifasi:
- Jami qarz, qarzdorlar soni, muddati o‘tgan qarz; kredit mavjud bo‘lsa alohida ortiqcha to‘lov ko‘rsatkichi.
- Ism/telefon qidiruvi va Xaridor qo‘shish. Filtrlar: Qarzi bor (default), Muddati o‘tgan, Yopilgan, Ortiqcha to‘lov, Barchasi. Due date ixtiyoriy; yo‘q bo‘lsa overdue emas. Do‘kon mahalliy sanasida muddat kunining oxiridan keyin overdue.
- Desktop jadval: ism, telefon, qarz, to‘lov muddati, to‘lov olish; yon panelda xaridor. Mobilda kartalar, bosilganda to‘liq sahifa.
- Xaridor: ism majburiy; telefon/izoh ixtiyoriy. Tarixda amal, summa, amal ortidan qoldiq, qurilma, sinxron holat, LP/RT/RV yoki payment identifikatori.
- Bo‘sh sahifa yangi xaridor qo‘shishga yo‘naltiradi; dasturdan oldingi qog‘oz qarzlar kiritilmagani ko‘rsatiladi. Naqd eski-daftar to‘lovi yangi hisobga adashib yozilmasligi uchun chek taqsimoti previewda ko‘rinadi.

Kassa:
- To‘lov oynasiga Nasiya; mobil 2x2 variant. Xaridorni qidirish/inline qo‘shish, oldingi qarz, savdo jami, hozir cash/card, readonly qarzga qoladi, ixtiyoriy muddat/izoh.
- To‘liq nasiya default cash/card 0. Tasdiq: “Sotish — X so‘m qarzga”. Jarayonda disable; retry ayni request. Kundalik naqd savdoda customer tanlash majburiy emas.
- Xaridor kartasidan Nasiyaga sotish kassaga tanlangan customer bilan o‘tadi; mavjud savatni jim tozalash yo‘q. Hold savat har birining customer/muddat holatini alohida saqlaydi.

To‘lov olish:
- Xaridor, ma’lum qarz, summa, Naqd/Karta/Aralash, oldest-first taqsimot yoki muayyan chek, qoldiq preview.
- Ma’lum qarzdan ortiq yangi input bloklanadi; offline stale qoldiqdan kelgan excess sinxronlashda yo‘qolmaydi.
- Lokal commitdan keyin payment raqami va oldin/keyin qoldiq. Bir bosish — bir payment. Printer/share xatosi paymentni bekor qilmaydi, qayta chop etish yangi payment yaratmaydi.
- Kredit bor bo‘lsa ko‘rinadigan alohida panel: Pulni qaytarish / Boshqa chekga qo‘llash (LAN kerak); avtomatik avans sarflash yo‘q.

Hisobot/cheklar:
- Chekda dastlabki to‘lov, yangi qarz, keyingi to‘lov, qaytarish, qolgan qarz. To‘lanmagan/Qisman/To‘langan va ortiqcha to‘lov belgisi.
- Qarzni undirish cashflow hisobotida o‘z to‘lov sanasi bilan; savdo sanasi o‘zgarmaydi. Savdo cheklari filtri ichiga paymentni soxta chek sifatida qo‘shmaslik.
- Davr [start inclusive, end exclusive), do‘kon timezone. Qarz qoldig‘i davr oxirigacha barcha tegishli eventlardan; faqat davrdagi savdolardan emas. Kech kelgan offline event hisobni yangilashi va tarixga ta’siri aniq.
- Kategoriya/ombor filtri savdo satrlariga. Qarz undirishning kategoriya/ombor bo‘yicha taqsimoti birinchi versiyada yo‘q: cashflowda bu filtrlar disabled, “Xaridor bo‘yicha to‘lov” deb tushuntiriladi; jim umumiy summani filtrlangan deb ko‘rsatmaslik.
- Nasiya savdo foydasi mavjud tarixiy revenue/cost bilan sale sanasida; to‘lovdagi real card fee payment sanasida, o‘sha vaqtdagi cached kurs bilan USD xarajati. Kurs yo‘q bo‘lsa USD unknown; joriy kurs bilan tarixni o‘zgartirmaslik. Bu mahsulot ichki hisob modeli, yangi soliq hisob siyosati emas.
- Xaridor ko‘chirmasi, Excel va printer UI bilan bir xil projectiondan foydalanadi. Xaridor/mijoz ma’lumotini ulashish faqat foydalanuvchi tashabbusi bilan.

## 8. Migratsiya, zaxira va tiklash

Room 13dan keyingi migratsiya additive; mavjud sale/customer munosabatini taxmin qilish yo‘q. Migratsiyadan oldin snapshot; fresh install, old Android DB va desktopdan olingan DB ham sinovda. Sxema identifikatori, FK, indekslar va schema nusxalari mos bo‘lishi kerak.

Snapshotga customers/accounts/events/lines/command receipts/outbox/inbox bir xil transaction holatida kiradi. Sotuv bo‘lmasa ham yangi payment zaxiraga tushadi. Telegramga ulashish offline bajarilmasligi mumkin, ammo lokal snapshot saqlanadi; Telegramga yetkazildi degan noto‘g‘ri tasdiq yo‘q.

Eski snapshotni tiklashda yangi offline amallarni yo‘qotishdan oldin recovery nusxa va aniq ogohlantirish. Tiklangan qurilmada yangi device instance GUID/sequence epoch; tarixiy event GUIDlar o‘zgarmaydi. Nusxadan ikkita telefon ishlasa ham yangi requestlar to‘qnashmaydi. Synced eventlar replayida idempotency saqlanadi. Desktop restore orqali server cursor ortga ketishi uchun history epoch/reset va full reconciliation kerak; bu aniqlanmasdan moliyaviy syncga ruxsat berilmaydi. Boshqa qurilmaga hech yetib bormagan va hech qayerga zaxiralanmagan yo‘qolgan eventni avtomatik tiklash mumkin emas.

## 9. Bosqichlar va release chegarasi

Har sessiya: kichik yakun → tegishli tekshiruv → commit/push → checkpoint. Limitning qolganini agent aniq o‘lchay olmaydi; yangi yirik ishni “ulgurarmiz” deb boshlamaslik. Quyidagilar sessiya hajmi bo‘yicha bo‘lingan, vaqt kafolati emas.

| Qism | Natija | Gate |
|---|---|---|
| 1 — joriy | Shu reja, test matritsasi, checkpoint | Hujjatlar main asosida alohida branchda, runtime kodi o‘zgarmagan |
| 2A — keyingi | Kotlin/C# sof pul va ledger hisob kontrakti + umumiy fixturelar | Rounding, allocation, excess, reversal arifmetikasi platformalarda bir xil |
| 2B-1 | Jadvallar va xavfsiz migratsiya | Fresh/upgrade, FK, snapshot, schema rollback |
| 2B-2 | Transactional repository/outbox | Request replay/hash, ownership, atomik sale/payment rollback |
| 3A-1 | Canonical wire component / validator | C#/Kotlin/Android fixture pariteti, buzilgan paket/identity/summa rad etilishi |
| 3A-2a | DB component export va atomic receiver | Real SQLite/Room replay, ownership, rollback va offline convergence |
| 3A-2b-1 | To‘liq frozen sale/items/stock/customer/event envelope codec/validator | C#/Kotlin/Androidda bir xil qat’iy tekshiruvlar |
| 3A-2b-2a | Durable inbox va atomic customer/payment receiver | Restart/replay/rollback, body conflict va capacity; sale opening pending |
| 3A-2b-2b | Concrete frozen sale/stock adapter + local freeze/preflight | Sale/items/stock/debt/body bitta commit; opening gate faqat testlardan keyin ochiladi |
| 3A-2c | Protocol capability, push/pull va ACK | ACK yo‘qolishi, eski peer, full/delta/restore yo‘llari |
| 3B | Parallel qurilmalar, restore epoch, kontakt konflikti | Convergence va restart/restore testlari |
| 4 | Desktop UI va cashier/payment integratsiyasi | Windows build + jarayon testlari |
| 5 | Mobil UI va offline cashier/payment | Android build, API 26/35 va jarayon testlari |
| 6A | Return/reversal/refund/kredit transfer | Aniq offset/refund va concurrent payment testlari |
| 6B | Hisobot, fee, Excel, receipt/statement | Davr/filtr/UZS/USD, double counting yo‘q |
| 7 | To‘liq backup/restore, regressiya va manual guide | Avtomatik suite + haqiqiy desktop/telefon sinovi |

Bosqich 4/5dagi UI development branchda; 6/7 bitmasdan productionga chiqmaydi. Mavjud nasiya hisobini yarim integratsiya bilan main’ga merge qilish yo‘q. 1-bosqichdan keyin foydalanuvchi 2A bosqichini davom ettirishga ruxsat berdi. Keyingi bosqichlar alohida sessiyalarda davom ettiriladi. Release tayyorlanganda AGENT.md bo‘yicha PROD joylashuvi qo‘llanadi; bu bosqich release yaratmaydi.

## 10. Qabul testlari

Quyidagilar rejalashtirilgan; hozir ishlatilgan yoki o‘tgan testlar deb hisoblamang.

| ID | Ssenariy | Kutilgan natija |
|---|---|---|
| D01 | To‘liq/qisman nasiya, cash/card/aralash | jami = paid + debt, bitta savdo/stock |
| D02 | Tugmani 2 marta, commitdan keyin restart | Bitta request/event, bir xil natija |
| D03 | Bir necha chekga qisman to‘lov | Saqlangan taqsimot yig‘indisi = payment; ortiqcha rounding yo‘q |
| D04 | 0, manfiy, juda katta son, 0.01, ortiqcha kasr | Valid minor unit yoki aniq rad; overflow yo‘q |
| D05 | 100k qarz, uzilgan ikki qurilmada 100k dan payment | 200k kirim, 100k kredit, ikkala event saqlanadi |
| D06 | Paketlar teskari tartibda, takror, uchinchi telefon | Oxirida bir xil ledger va qoldiq |
| D07 | Server commit, ACK yo‘q; qayta yuborish | Ikkinchi pul/stock effekti yo‘q |
| D08 | Bir GUIDga o‘zgargan body | Integrity error; avvalgi event o‘zgarmaydi |
| D09 | Savdo/stock/qarz guruhining o‘rtasida xato | Hammasi rollback; partial ACK yo‘q |
| D10 | 250/500 chegarasi, bitta katta savdo | Guruh bo‘linmaydi; unsyncable committed sale yo‘q |
| D11 | Narx konflikti va mustaqil payment | Payment sync davom etadi; konflikt data yo‘qolmaydi |
| D12 | Eski peer, noto‘g‘ri store/server identity | Aniq holat, yangi nasiya oddiy sale bo‘lib oqib ketmaydi |
| D13 | 100k sale/20k paid; 30k va 90k return | Mos ravishda debt 50k/refund 0 va debt 0/refund 10k |
| D14 | Return quote orasida payment; keyin offline payment | Requote; kech event saqlanadi, zarur kredit ko‘rinadi |
| D15 | Return reversal, payment reversal ikki qurilmadan | Bitta authority natijasi, aniq teskari ta’sir |
| D16 | Kredit refund/transfer qayta bosiladi | Bir marta; source/target/pul jami mos |
| D17 | Nasiya bugun, payment ertaga | Bugungi sale/profit bir marta; ertaga cashflow va real fee |
| D18 | Davr chegarasi, oldingi qarz, kech sync | Opening + changes = closing; timezone aniq |
| D19 | Kategoriya/ombor/BRAK/return filtrlari | Savdo filtri to‘g‘ri; collection chalg‘itmaydi |
| D20 | Mahsulot/xaridor nomi yoki kursi o‘zgardi | Tarixiy snapshot/fee kursi saqlanadi |
| D21 | Room13 upgrade, fresh install, desktop backup restore | Ma’lumot yo‘qolmaydi, FK/schema mos |
| D22 | Faqat payment bo‘ldi, sotuv yo‘q; backup/restore | Payment, taqsimot va outbox nusxada bor |
| D23 | Snapshot ikki telefonga, eski desktop snapshot | Eventlar dedup; yangi device identity; cursor reset reconciliation |
| D24 | Bir xil ism/telefon offline yaratildi, keyin archive | Avtomatik noto‘g‘ri merge yo‘q; moliyaviy event saqlanadi |
| D25 | 360dp, katta shrift, keyboard, tab/back, hold cart | Maydon/tugmalar ochiq, savat/customer saqlanadi |
| D26 | Printer/share muvaffaqiyatsiz, qayta chop | Payment takrorlanmaydi |
| D27 | Oddiy savdo, manfiy stock, BRAK, mavjud return | Avvalgi regressiya suite o‘tadi |

Avtomatika: umumiy JSON fixturelar bilan Kotlin va C# unit testlar; haqiqiy SQLite transactional testlar; mavjud WifiSync core test infratuzilmasida ikki nusxa/replay; Android API26/35 Room instrumentatsiya. Windows/telefon/printer/Telegram haqiqiy sinovlarini agent bajarmagan bo‘lsa alohida belgilang. Faqat hujjat o‘zgargan 1-bosqich uchun ilova buildi shart emas.

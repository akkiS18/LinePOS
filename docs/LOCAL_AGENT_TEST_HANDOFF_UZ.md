# LinePOS — lokal agent uchun qolgan testlar

## Vazifa va chegaralar

Repo: `akkiS18/LinePOS`. Ishchi branch: `fix/debt-stage3-complete`.
Ushbu qo‘llanmadan oldingi commit: `176eb3315770659943ac59ef194f880d19c431ff`.

Vazifa: quyidagi testlarni bosqichma-bosqich bajarish, aniq xatolarni ajratish, zarur tuzatishlarni shu test branchida bajarish va har bir bosqich natijasini yozish. Test muvaffaqiyatini taxmin qilmang. Compile, unit/integration, real qurilma va UI tekshiruvlarini alohida qayd eting. O‘tmagan yoki bajarilmagan testlarni yashirmang.

UI/UX dizaynini, Firebase litsenziyalashni va CBU kurs olishni o‘zgartirmang. Wi-Fi sinxron tashqi serversiz ishlaydi. Manfiy ombor qoldig‘i ataylab ruxsat etilgan. Qarz faqat xaridorlardan do‘konga, faqat so‘mda.

Main branchga merge/push yoki release APK/EXE chiqarish ushbu topshiriqqa kirmaydi. Foydalanuvchining boshqa lokal o‘zgarishlarini o‘chirmang. Bazalarni tozalash, ilovani uninstall qilish yoki sync navbatini ommaviy o‘chirish yechim emas.

## 0. Muhit va ishni olish

Avval `git status`ni tekshiring. Lokal o‘zgarishlar bo‘lsa xavfsiz saqlang yoki alohida worktree ishlating; `reset --hard` ishlatmang.

```powershell
git fetch origin
git switch fix/debt-stage3-complete
git pull --ff-only origin fix/debt-stage3-complete
git log -5 --oneline
dotnet --info
java -version
```

.NET 8 SDK, loyiha talab qilgan JDK/Android SDK va Gradle bog‘liqliklari kerak. Gradle wrapper versiyasi `gradle/wrapper/gradle-wrapper.properties`da. Faqat runtime yetarli emas. SDK yo‘li va mavjud lokal secrets/signing fayllarini saqlang, Gitga kiritmang.

`AGENT.md` va quyidagi hisobotlarni o‘qing:

- `docs/DEBT_ZERO_DELTA_TEST_RESULTS_UZ.md`
- `docs/DEBT_TEST_STAGE2_RESULTS_UZ.md`
- `docs/DEBT_FIX_STAGE1_MOBILE_RETURNS_UZ.md`
- `docs/DEBT_FIX_STAGE2A_DESKTOP_RETURNS_UZ.md`
- `docs/DEBT_FIX_STAGE2B_RETURN_REFRESH_UZ.md`
- `docs/DEBT_FIX_STAGE3_RECOGNISED_PROFIT_UZ.md`
- `docs/DEBT_FIX_STAGE4_LAN_RECONNECT_UZ.md`

## Hozirgi dalillar

Cloud muhitida SQLite trigger va mavjud qaytarish yozuvlari tekshirildi, lekin ilovalar build qilinmadi. Android Gradle yuklashda tarmoq xatosi, .NET esa compilerga yetmasdan Process.GetStat/Get_StartTime xatosi berdi. Bu runtime testlar PASS degani emas. Bu muhit xatolarini ilovadagi bug deb qabul qilmang.

Mavjud sxema suite’i 7/7 o‘tdi. Yuborilgan zaxiralardagi desktop 3 ta va mobil 2 ta qaytarishning chek/tannarx/naqd/karta/reversal bog‘lanishlari mos chiqdi. Bu yangi qaytarishlarni bajarib tekshirish o‘rnini bosmaydi.

## 1. Oldingi bosqichlardan qolgan majburiy testlar

### 1A. Build

Repo ildizidan:

```powershell
.\gradlew.bat :app:compileDebugKotlin :app:compileDebugAndroidTestKotlin --console=plain
dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj
```

Xato chiqsa birinchi haqiqiy sababni tuzating, faqat tegishli buildni qayta ishga tushiring. Test loyihalaridagi source link/resource yo‘llari ham to‘g‘ri ekanini tekshiring. Build o‘tishi UI ishlashini tasdiqlamaydi.

### 1B. Nol qoldiq amali sinxronni bloklagan holat

Foydalanuvchi bergan fayllar:

- Desktop: `SMARTKassa_Baza_2026-10-14_1318.db`
- Mobil: `LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db`

Fayllar repoga kiritilmagan. Foydalanuvchining lokal zaxira papkasidan toping yoki undan oling. Asllarni ReadOnly saqlang; amaliy testlar uchun nusxa oling. Misoldagi `C:\backups` yo‘lini haqiqiy yo‘l bilan almashtiring.

Aniqlangan holat: ikkala bazada 13 ta bir xil GUIDli tovar va 5 ta bir xil GUIDli ombor. Mobil navbatda 7 ta `stock` amal, hammasida Delta=0. `seq=48` amaldagi tovar GUIDi `18344632-4ff3-4d46-a888-fc5aac41280a` ikkala bazada yo‘q. Desktop oldin shu nol amalni rad etib, keyingi pullni bloklagan. Yetishmayotgan desktop qaytarish GUIDi `64f35e30-0c91-496f-83f6-6b4f810e83b4`.

`edcc95c` tuzatishi: desktop nol deltani biznes qoldig‘iga tegmasdan ACK qiladi; original op ID/payload saqlanadi. Yangi nol insert/delete amallari ikki klientda trigger orqali yozilmaydi. `1b56ecf` umumiy sxemani moslashtirdi va testlar qo‘shdi.

```powershell
dotnet run --project tests/WifiSync.ZeroDeltaTests -- "C:\backups\SMARTKassa_Baza_2026-10-14_1318.db" "C:\backups\LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db"
python tests/check_zero_stock_triggers.py "C:\backups\SMARTKassa_Baza_2026-10-14_1318.db" "C:\backups\LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db"
python tests/test_sync_schema.py
```

C# suite aynan berilgan zaxiralarga mos, 7 pending va 3 desktop return kutadi. O‘zgargan bazada test assertionini shunchaki PASS qilish uchun yumshatmang.

PASS: 7 ACK; replay yangi yozuv/qoldiq yaratmaydi; bir xil ID bilan boshqa payload rad etiladi; nol bo‘lmagan mavjud bo‘lmagan tovar rad etiladi; xatoli aralash paket atomik rollback; pull yetishmayotgan qaytarish va chekni qaytaradi. Biznes jadvallari nol pushdan o‘zgarmaydi.

Keyin haqiqiy ilovalarning test bazalarida sinxron qiling: mobil navbat tasdiqlansin, qaytarish cheki telefonda ko‘rinsin, takror reconnectda dublikat bo‘lmasin. C# store testi HTTP va Android snapshot importini tekshirmaydi — bu qadam alohida shart.

### 1C. Qaytarish va qaytarishni bekor qilish

```powershell
dotnet run --project tests/Business.CoreTests -- --returns
python tests/check_return_backup_invariants.py "C:\backups\SMARTKassa_Baza_2026-10-14_1318.db" "C:\backups\LinePOS_1791965861306_5fc3d5ab-26ff-454c-a820-b134a2d38b22.db"
```

`--returns` faqat mavjud ReturnAccountingTests, ReturnStoreTests, DebtReturnAndReversalTests suite’larini tanlaydi. Ularning kompilyatsiyasi cloud muhitida tasdiqlanmagan.

Ilovalarda quyidagilarni ham tekshiring:

1. Mobil Qaytarish tugmasi crash qilmasin; logcatdagi exceptionni yozing.
2. Qisman, qolgan qismini to‘liq, kasr miqdor, sifatli va brak qaytarish.
3. 0/manfiy/ortiqcha miqdor va noto‘g‘ri naqd+karta yig‘indisi rad etilsin.
4. Tez ikki marta bosish, serverda commitdan keyin javob yo‘qolishi, ilova restartidan keyin ayni request bilan qayta tekshirish: bitta moliyaviy amal.
5. Ikki qurilmada bir vaqtda qaytarish: jami sotilgan miqdordan oshmasin.
6. Panel ochiq paytda boshqa qurilmada qarz to‘lovi yoki qaytarish: yangi quote ko‘rsatilsin, o‘zgargan hisob foydalanuvchi qayta tasdiqlamaguncha yuborilmasin.
7. Aniq rad etilgan draftni tahrirlash mumkin bo‘lsin; natijasi noma’lum submitted draft yangi request ID bilan qayta yuborilmasin.
8. Qaytarishni bekor qilish bir marta ishlasin, takror bosishda qo‘shimcha pul/qoldiq yaratmasin.
9. Asl chek tarixi o‘zgarmasin; RT/RV cheklar desktop va telefonda bir xil raqam bilan topilsin.
10. Nasiya qaytarish avval qarzni kamaytirsin, ortiqcha to‘langan qismigina refund bo‘lsin; ikkala bazadagi qarz va omborni solishtiring.

## 2. Qolgan 3-bosqich — nasiya foydasi va hisobot

Asosiy batafsil jadval: `docs/DEBT_FIX_STAGE3_RECOGNISED_PROFIT_UZ.md` (22 manual holat). Undagi barcha holatlarni bajaring; bu yerda muhim mezonlar jamlangan.

Yangi kelishilgan qoida: foyda kelib tushgan pul ulushiga qarab tan olinadi. Eski testlar to‘liq nasiya foydasini savdo kunida kutsa, ularni biznes qoidaga mos dalil bilan yangilang; ishlab chiqarish kodini eski noto‘g‘ri kutilmaga moslamang.

Bazaviy fixture: sotuv 100 000 so‘m, tannarx 70 000 so‘m, komissiyasiz:

| Amal | Kutiladigan foyda |
|---|---|
| To‘liq nasiya, to‘lov 0 | 0 |
| 20 000 to‘lov | 6 000 jami |
| Yana 30 000 to‘lov | 9 000 qo‘shimcha, 15 000 jami |
| Qolgan 50 000 to‘lov | 15 000 qo‘shimcha, 30 000 jami |
| To‘lovni bekor qilish | Tegishli foyda teskari qaytadi |

Keyingi to‘lov foydasi **to‘lov sanasida** hisoblanadi. Sotuv daromadi va sotilgan miqdor keyingi to‘lovda qayta qo‘shilmaydi. Bir xil to‘lov boshqa qurilmadan kelganda dublikat bo‘lmasin.

Qo‘shimcha majburiy holatlar:

- Boshlang‘ich naqd/karta avansi, keyingi bir nechta to‘lov, bir to‘lovning bir necha qarzga taqsimlanishi.
- Komissiya bir marta ayirilishi; qaytarilgan komissiya va to‘lov reversali.
- Tarixiy USD: savdo kursi 10 000, to‘lov 20 000, komissiya 400 va uning kursi 12 000 bo‘lsa UZS foyda 5 600; USD = 6 000/10 000 - 400/12 000 = 0.566666… . Joriy CBU kursini o‘zgartirish tarixni o‘zgartirmasin.
- Kurs yo‘q bo‘lsa mavjud hujjatlashtirilgan unknown holati; joriy kursdan yashirin fallback bo‘lmasin.
- To‘lanmagan savdoni sifatli to‘liq qaytarish: foyda 0. Brak to‘liq qaytarish: tannarx yo‘qotishi -70 000.
- 20 000 to‘langan savdoni sifatli to‘liq qaytarish: oldingi +6 000 ga qaytarishning -6 000 ta’siri. Asl chek va RTda ikki marta ayirilmasin.
- Qaytarishni bekor qilish, avans/kreditni refund qilish va boshqa qarzga ko‘chirish.
- Sana, kategoriya, ombor, savdo/brak/qaytarish filtrlari va chek qidiruvi.
- Desktop/mobil cards, chek tafsiloti va Excel bir xil hisobni bersin. Foyda-only satrlar son/miqdor/daromadni oshirmasin.
- Kasr miqdor va tiyin yaxlitlashida to‘liq yakuniy summa mos bo‘lsin.
- Mahsulot nomi/kategoriyasi/kursini keyin o‘zgartirish tarixiy hisobotni o‘zgartirmasin.

Mavjud Business.CoreTests/WifiSync.CoreTests suite’larini ko‘rib, bu yangi cash-proportional projection uchun haqiqiy production kodini chaqiradigan kerakli kichik testlarni qo‘shing. Faqat formulani testda qayta yozib, uning o‘zini tekshirish yetarli emas. Keng suite’larni faqat aniq regressiya sababli ishga tushiring.

## 3. Qolgan 4-bosqich — Wi-Fi va yakuniy regressiya

Asosiy jadval: `docs/DEBT_FIX_STAGE4_LAN_RECONNECT_UZ.md` (A1–A15). Ikkala klient yangi buildda bo‘lsin.

Windows ishonchli do‘kon tarmog‘i Private; mavjud Firewall ruxsati tugmasi bilan TCP va UDP 8080 uchun private/local-subnet ruxsati. Firewallni butunlay o‘chirmang. AP isolation/broadcast blokini ilova xatosidan ajrating.

Majburiy testlar:

1. Birinchi QR ulanish va ikki tomonlama mahsulot/savdo/qarz sinxroni.
2. Internetsiz Wi-Fi + mobil data yoqilgan telefon. Tashqi server talab qilinmasin.
3. Wi-Fi off: status Ulanmoqda/Jonli orasida miltillamasin; amallar offline saqlansin.
4. Desktop IPni haqiqatan o‘zgartiring (`ipconfig` bilan tekshiring). QR/IP kiritmasdan aynan avvalgi desktop topilsin.
5. IP almashishdan oldingi offline navbat yo‘qolmasin va dublikat bo‘lmasin.
6. Desktop/telefon restart, sync serverini tez stop/start, yangi IPni saqlash.
7. Shu desktopning eski QRi: saqlangan token amalda bo‘lsa qayta pairing/kursor reset bo‘lmasin.
8. UDP bloklanganda qo‘lda yangi IP/QR yo‘li; keyin ruxsatni qaytaring.
9. Konflikt paytida IP almashish: konflikt yo‘qolmasin, foydalanuvchi tanlovidan keyin davom etsin.
10. Ruxsat bekor qilinsa avtomatik yangi token olinmasin; begona desktopga navbat yuborilmasin.
11. Test zaxirasidan desktop bazasini tiklash: epoch recovery, pending saqlanishi, GUIDlar va qoldiq/qarz dublikat bo‘lmasligi. Token eski zaxirada yo‘q bo‘lsa qayta QR kerakligi normal holat bo‘lishi mumkin.
12. Qaytarish va qarz to‘lovining javobi uzilgandan keyingi replay: faqat bitta effekt.

Discovery production fayllari: `LanDesktopDiscovery.kt`, `LanDiscoveryResponder.cs`, `LocalSyncManager.kt`, `LocalSyncServer.cs`. HMAC/nonce/server ID tekshiruvi va redirectga yo‘l qo‘ymaslikni saqlang. Tokenlarni logga yozmang. Simulyatsiya real Android/Windows LAN sinovining o‘rnini bosmaydi.

## Yakuniy kichik regressiya

- Oddiy naqd/karta/aralash sotuv, savat yig‘indisi va takror bosish himoyasi.
- Manfiy qoldiq bilan sotish va ikki ombor o‘rtasida o‘tkazish.
- Nasiya ochish, qisman to‘lov, to‘lov reversali, credit refund/transfer.
- Chek raqami bilan ikkala qurilmada qidirish.
- Tarixiy nom bilan Excel, filtrlar va jami summalar.
- Telefon DB zaxirasini share orqali Telegramga tayyorlash; haqiqiy kontaktga yuborishni foydalanuvchi bajarsin. Zaxira yaxlitligi va alohida test muhitida restore.
- Firebase/CBU o‘zgarmaganini diff orqali tekshirish; real litsenziyani o‘chirib tajriba qilmang.

## Natijani qanday topshirish

Har bosqich uchun bitta jadval yozing: test ID, commit, qurilma/muhit, aniq komanda yoki qadam, kutilgan, haqiqiy, PASS/FAIL/BLOCKED/NOT RUN. Bir xil bazaviy ma’lumotni ishlating; raqamlarni qayd eting.

Bug bo‘lsa: takrorlash qadami → sabab → eng kichik tuzatish → tegishli regression testi → natija. Lokal boshqa ishlarni commitga qo‘shmang. DB, token, signing kalitlari va shaxsiy ma’lumotlarni repoga push qilmang.

Yakuniy hisobot: `docs/LOCAL_AGENT_TEST_RESULTS_UZ.md`. Qaysi test real qurilmada, qaysi biri avtomatik, qaysi biri faqat kod tahlili ekanini aniq yozing. Barcha bosqichlarni boshlashdan oldin build muhitini ishlating; muhit bloklansa qolgan runtime testlarni PASS demang. Main merge uchun foydalanuvchiga natijani ko‘rsating.

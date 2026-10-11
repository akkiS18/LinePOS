# 2A bosqich — mijoz tahrirlarini offline sinxronlash

## Holat va chegara

Branch: `fix/debt-stage2a-customer-sync`. 1-bosqichning `f3817e0` commitidan davom etadi; o‘sha hisoblash tuzatishlari ham mavjud. Main bilan birlashtirilmagan.

Bu bosqich mijoz nomi, telefoni, izohi va bloklash/blokdan chiqarish amallarini qamrab oladi. UI joylashuvi o‘zgarmadi; mavjud sinxron holati matnida navbat/konflikt ko‘rinadi. Moliyaviy amallar transporti, dastlabki ulanish va qoldiq muammolari keyingi bosqichlarda.

Kod ko‘rib chiqildi. Build, CI, emulator va avtomatik testlar ishga tushirilmadi. Quyidagi manual tekshiruvlar hali bajarilmagan; production tayyorligi tasdiqlanmagan.

## Nima o‘zgardi

- Telefonda mijoz tahriri va doimiy navbat bir bazaviy tranzaksiyada saqlanadi.
- Har bir tahrir o‘z RequestGuid identifikatoriga ega. Desktop o‘zgarish bilan birga qabul kvitansiyasini saqlaydi; yo‘qolgan javob sabab qayta yuborilgan so‘rov takror qo‘llanmaydi.
- Telefon faqat tasdiqlangan so‘rovni navbatdan chiqaradi. Pending mijozni desktop snapshoti ustidan yozmaydi.
- Turli maydonlardagi parallel tahrirlar birlashadi. Bir maydon turlicha tahrirlansa, lokal qiymat va navbat saqlanadi, konflikt xabari chiqadi.
- Konflikt kuzatilganidan keyin telefonda qayta saqlash telefonning joriy qiymatlarini tanlaydi. Bu barcha kontakt maydonlariga taalluqli: saqlashdan oldin nom, telefon, izoh va blok holatini tekshiring.
- Desktopdan kelgan blok holati ham boolean, ham 0/1 formatida o‘qiladi.
- Eski desktop yangi protokolni bilmasa tahrirlar o‘chirilmaydi; yangilash xabari chiqadi.

## Manual test uchun tayyorgarlik

Ikkala dasturni shu branchdan build qilib o‘rnating. APK/EXE bu bosqichda tayyorlanmadi. Mavjud bazaning zaxirasini oling va nusxada tekshiring. Avvaldan bir-biriga ulangan desktop va telefon ishlating: dastlabki ulanish muammosi bu bosqichga kirmaydi.

Bitta sinov mijozini ikkala qurilmada oching. Boshlang‘ich nom, telefon, izoh, blok holati va qarz summasini yozib oling.

## Tekshiruvlar

1. **Oddiy tahrir.** Telefonda nom, telefon va izohni o‘zgartiring. Desktopda aynan shu qiymatlar chiqishi, yangi dublikat mijoz paydo bo‘lmasligi va navbat bo‘shashi kerak.
2. **Offline navbat.** Wi-Fi ni uzing. Nomni ikki marta, telefonni va izohni o‘zgartiring; mijozni bloklang. Telefon dasturini majburan yoping va qayta oching. Lokal qiymatlar saqlanishi kerak. Wi-Fi qaytganda desktopga oxirgi qiymatlar va blok holati yetib borishi kerak. Keyin blokdan chiqarishni ham tekshiring.
3. **Desktopdan bloklash.** Desktopda bloklang, telefonda yangilanishini kuting. Blok holati to‘g‘ri chiqsin; desktopda blokdan chiqarilganda telefonda ham ochilsin.
4. **Turli maydonlar.** Aloqa uzilganida telefonda faqat nomni, desktopda faqat izohni o‘zgartiring. Ulangach ikkala o‘zgarish ham ikkala qurilmada qolishi kerak.
5. **Bir maydon konflikti.** Aloqa uzilganida telefon va desktopda nomni ikki xil qiymatga almashtiring. Ulangach telefon tahriri yo‘qolmasin, konflikt xabari ko‘rinsin. Telefonda mijozni tahrirlash oynasini qayta ochib, barcha qiymatlarni tekshiring va saqlang. Desktop shu qiymatlarga kelishi, navbat bo‘shashi kerak. Kontakt konflikti boshqa sinxron amallarni to‘xtatmasligini ham tekshiring.
6. **Uzilish va qayta yuborish.** Saqlash paytida Wi-Fi ni uzib-qayta ulang. Dublikat mijoz bo‘lmasin, navbat keyinchalik bo‘shasin. So‘ng desktopda yana tahrirlang va qayta ulang: eski telefon so‘rovi desktopning keyingi tahririni ortga qaytarmasin. Javob yo‘qolishining aynan kerakli vaqtini manual usulda kafolatlash qiyin; bu test idempotentlikning to‘liq isboti emas.
7. **Yangi offline mijoz.** Avvaldan ulangan telefonda offline yangi mijoz yarating va uni tahrirlang. Ulangach avval mijoz yaratilishi, so‘ng tahrirlari yetib borishi kerak.
8. **Eski desktop bilan moslik (ixtiyoriy, baza nusxasida).** Yangi telefonni eski desktopga ulang va mijozni tahrirlang. Navbat yo‘qolmasligi kerak. Desktop ham yangilangach tahrir yetib borsin.
9. **Pulga ta’sir yo‘q.** Kontakt tahrirlaridan oldin va keyin mijoz qarzi, to‘lovlari va savdolari summasi bir xil qolishi kerak.

Xato bo‘lsa bazani yoki ilova ma’lumotlarini tozalamang. Test raqami, qaysi qurilmada nima qilinganligi, kutilgan/amaldagi qiymatlar va sinxron xabarining skrinshotini yuboring.

## Keyingi ish

Bu bosqichning manual natijalariga qarab kerakli tuzatishlar qilinadi. Qarz pul amallarining takror so‘rov himoyasi/to‘liq transporti hamda ulanish-qoldiq bosqichi hali alohida ish bo‘lib qoladi.

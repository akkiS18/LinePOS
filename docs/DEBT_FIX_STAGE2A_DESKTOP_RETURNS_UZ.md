# 2A — desktop qaytarish so‘rovini tiklash

Branch: `fix/debt-stage3-complete`. Ushbu commit 1-bosqich (`e58a978`) ustiga yozilgan.

## Bajarilgan qism

- Oldingi so‘rovni takror tekshirish joriy qarz balansi yoki qaytarilgan miqdor previewsi bilan bloklanmaydi. Avval mavjud request natijasi bazadan olinadi.
- Serverning qaytarish tranzaksiyasi rad etilgach, alohida tranzaksiyada shu RequestGuid bilan qaytarish saqlanmaganligi, baza identifikatori va aynan shu draft tekshiriladi. Faqat shu shartlarda draft yechiladi va tahrirlash qayta ochiladi.
- Saqlangan qaytarish yoki natijasi noma’lum texnik xato bo‘lsa, request/payload saqlanadi; yangi ID ajratilmaydi. Baza tekshiruvi ishlamasa ham draft o‘chirilmaydi.
- Aniq rad etilgandan keyin balans/qolgan miqdor yangilanadi. Yangi amal foydalanuvchi qayta tasdiqlamaguncha bajarilmaydi.
- Saqlangan draft ochilganda pul qiymatlari preview tomonidan ustidan yozilmaydi.
- Noto‘g‘ri son matni avtomatik 0 bo‘lmaydi; boshlang‘ich 0 preview qarz hisoblash xatosini ko‘rsatmaydi.

UI joylashuvi o‘zgarmadi. Mobil kod, foyda hisobi va IP topish mexanizmiga bu bosqichda tegilmadi. Build/CI/avtomatik test bajarilmadi, EXE yaratilmagan. Faqat kod va diff ko‘rib chiqildi.

## Manual test tartibi

Test bazasini va oxirgi branchdan yig‘ilgan desktopni ishlating. Mavjud toza test checkoutda:

```powershell
git switch fix/debt-stage3-complete
git pull --ff-only origin fix/debt-stage3-complete
git log -1 --oneline
```

Saqlanmagan lokal o‘zgarishlar bo‘lsa reset/force ishlatmang; alohida papkaga clone qiling. Kod papkasining alohidaligi bazaning alohidaligini bildirmaydi.

| № | Amal | Kutiladigan natija |
|---|---|---|
| 1 | Desktopda nasiya qaytarish oynasini ochib, miqdorni 0 qoldiring. | Preview 0 ko‘rsatadi; eski generic qarz xatosi chiqmaydi. |
| 2 | 21 000lik chek, 11 000 qarz qoldig‘i uchun to‘liq qaytarishga naqd 21 000 kiriting. | Amal rad etiladi; moliyaviy yozuv/stock o‘zgarmaydi. Maydonlar ochiq qoladi va 11 000 qarz/10 000 pul taqsimoti yangilanadi. 10 000 pul bilan qayta tasdiqlang — bitta qaytarish. |
| 3 | Qaytarish oynasini ochiq qoldiring. Telefonda shu qarzga qo‘shimcha to‘lov qilib, desktopga yetishini kuting. Eski summa bilan tasdiqlang. | Mos kelmagan summa rad etiladi, yangi balans o‘qiladi. Qayta tasdiqlagach bitta qaytarish; so‘rov tiqilib qolmaydi. |
| 4 | Sababni bo‘sh qoldirib tasdiqlang, keyin sabab yozing. Alohida testda miqdorni mavjudidan ortiq kiriting va tuzating. | Har bir rad etishdan keyin tahrirlash mumkin. Faqat to‘g‘ri tasdiq moliyaviy yozuv yaratadi. |
| 5 | Miqdor/pulga harf, bo‘sh matn yoki manfiy son kiriting. | Xabar chiqadi, qiymat nolga aylanmaydi, draft keraksiz bloklanmaydi. |
| 6 | Muvaffaqiyatli qaytarishdan keyin shu chekning qolgan qismini qaytaring. | Oldingi so‘rov tozalangan; qolgan miqdor bilan yangi qaytarish mumkin. |

### Lokal agent bilan texnik manual tekshiruv

Quyidagilar faqat test bazasida debugger yoki nazoratli nosozlik kiritish bilan bajariladi. Haqiqiy mijoz bazasini o‘zgartirmang.

1. **Commitdan keyingi uzilish:** `CommitDesktop` natija berganidan keyin, `FinishDesktopDraft` natijani yozishidan oldin jarayonni to‘xtating. Qayta oching va oldingi natijani tekshiring. Joriy qaytarilgan miqdor/qarz balansi o‘zgargan bo‘lsa ham asl RT GUID qaytsin; ikkinchi qaytarish, stock yoki qarz yozuvi yaratilmasin.
2. **Commitdan oldingi uzilish:** draft yozilgach jarayonni to‘xtating. Qayta ochib shu request bilan davom eting. Jami bitta qaytarish bo‘lsin.
3. **Saqlangan natija:** `FinishDesktopDraft(requestId, result)` dan keyin, tasdiq dialogidan oldin jarayonni to‘xtating. Qayta ochib natijani tekshiring — shu GUID, takror chiqim yo‘q.
4. **Baza band yoki o‘qish xatosi:** draft saqlanganidan keyin baza xatosini keltirib chiqaring. RequestGuid/payload o‘chmasin yoki yangilanmasin. Xato bartaraf etilgach shu request davom etsin.
5. **Saqlanib qolgan eski rad etilgan draft:** oldingi versiyada qarz balansi o‘zgargani sabab bloklangan draftni yangi versiyada oching. Natijani tekshirganda u haqiqatan saqlanmagan bo‘lsa maydonlar ochilsin; tuzatib yakunlash mumkin bo‘lsin.
6. **Rad etilgandan keyingi quote xatosi:** rad etish isbotlanib draft yechilganidan keyin quote o‘qishni xatoga uchrating. Eski hisob bilan tasdiqlanmasin; oynani qayta ochish talab etilsin.

## Qolgan ish

2B: ikkala platformada yangi qaytarishni tasdiqlashdan oldin dolzarb hisobni olish va o‘zgargan taqsimotni qayta tasdiqlatish. 2A faqat rad etilgandan keyingi tiklanish va oldingi so‘rov natijasiga yetib borishni tuzatadi.

3-bosqich foyda qoidasi va 4-bosqich yangi IPni topish ham qolgan. Main merge qilinmagan; ushbu natijalar testdan o‘tgan degani emas.

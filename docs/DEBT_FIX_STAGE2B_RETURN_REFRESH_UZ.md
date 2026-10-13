# 2B — qaytarishni tasdiqlashdan oldin hisobni yangilash

Branch: `fix/debt-stage3-complete`. 2A (`9076e8c`) ustiga yozilgan.

## Bajarilgan qism

Desktop va telefon yangi qaytarishni yuborishdan oldin desktop bazasidagi joriy hisobni oladi. Qarz balansi, oldingi qaytarilgan miqdor/summa yoki qolgan komissiya o‘zgargan bo‘lsa, birinchi bosish qaytarishni bajarmaydi: yangi hisob ko‘rsatiladi va qayta tasdiqlash talab qilinadi. Miqdor va naqd/karta maydonlari yashirincha o‘zgartirilmaydi. Kiritilgan summa yangi hisobga mos bo‘lmasa, tuzatish kerak.

Telefonda taqqoslash raqam qiymatiga asoslangan: 10000 va 10000.00 yoki JSON maydonlari tartibi o‘zgarishi ortiqcha qayta tasdiq chiqarmaydi. Qolgan komissiya lokal hisobda ham bor va kiritilgan fee shu chegaradan oshmasligi tekshiriladi.

Oldin yuborilgan, natijasi noma’lum so‘rovlar yangi quote talab qilmasdan o‘sha RequestGuid bilan qayta tekshiriladi. Offline draft saqlash mavjud; yangi qaytarishni yakunlash uchun desktop bilan aloqa kerak.

Yakuniy server tranzaksiyasi ham miqdor va pul taqsimotini qayta tekshiradi. Quote olish va saqlash orasida yangi o‘zgarish yuz bersa, mos kelmaydigan amal rad etiladi; 2A desktop tiklanishi va mobil rad etilgan draftni tahrirlash yo‘li ishlatiladi. Quote butun tranzaksiya davomida bazani qulflab turmaydi.

UI joylashuvi, Firebase, CBU, foyda hisoblash va IP topish o‘zgarmadi. Build, CI, emulator va avtomatik test bajarilmadi; APK/EXE yaratilmagan. Kod/diff bir marta ko‘rib chiqildi. Main merge yo‘q.

## Manual test

Ikkala dasturni oxirgi `fix/debt-stage3-complete` branchdan build qiling. Test bazalaridan foydalaning. 1 va 2A qo‘llanmalaridagi crash/takror request testlari ham amal qiladi.

| № | Amal | Kutiladigan natija |
|---|---|---|
| 1 | Desktopda 21 000lik chek, 11 000 qarz qoldig‘ining qaytarish oynasini oching; hammasini belgilang (10 000 pul qaytarish). Telefonda shu qarzga 5 000 to‘lang va desktopga yetishini kuting. Desktopda tasdiqlang. | Birinchi bosishda qaytarish yozilmaydi. Yangi hisob: 6 000 qarzdan, 15 000 pul. Eski 10 000 pul maydoni yashirincha o‘zgarmaydi. 15 000ga tuzatib qayta tasdiqlash — bitta qaytarish. |
| 2 | Xuddi shu holatni aksincha qiling: telefon qaytarish oynasi ochiq, desktopda qarz to‘lovi. | Telefonda birinchi tasdiq yangilangan hisobni ko‘rsatadi, ammo request yuborilmaydi. Tekshirib qayta tasdiqlashdan keyin bitta qaytarish. |
| 3 | Bir qurilmada qaytarish oynasini ochib, boshqasida shu chekdan bir qismini qaytaring. Keyin eski oynada tasdiqlang. | Qaytgan/qolgan miqdor yangilanadi. Eski ortiqcha miqdor bilan amal bajarilmaydi; miqdorni tuzatish mumkin. |
| 4 | Hech narsa o‘zgarmagan oddiy naqd va nasiya cheklarida qaytarishni tasdiqlang. | Ortiqcha “hisob o‘zgardi” chiqmaydi. Bitta to‘g‘ri tasdiq yetadi. |
| 5 | Naqd/karta aralash taqsimot kiriting. Hisob yangilanganda maydonlarni kuzating. | Karta summasi yashirincha naqdga aylantirilmaydi; kassir yangi jami summaga moslab tuzatadi. |
| 6 | Telefonda oynani ochgach Wi-Fi ni uzing va yangi qaytarishni tasdiqlang. Keyin qayta ulang. | Quote olinmasa yangi qaytarish yuborilmaydi. Aloqa tiklangach qayta tekshiradi. Offline draftni alohida saqlash mumkin. |
| 7 | Komissiyali test chekida boshqa qurilmadan qisman qaytarish va fee reversal qiling. Eski oynani tasdiqlang. | Qolgan komissiya o‘zgarishi aniqlanadi; ortiqcha fee bilan amal o‘tmaydi. |
| 8 | Yangi hisob ko‘rsatilgach, qayta tasdiqlashdan oldin yana qarz to‘lovi qiling. | Keyingi bosishda yana o‘zgarish aniqlanadi; eskirgan hisob bilan yuborilmaydi. |
| 9 | To‘liq offline draft saqlang; shu vaqt desktopda balansni o‘zgartiring. Telefonni ulab draftni oching va tasdiqlang. | Dolzarb hisob bilan tekshiriladi; pul miqdori mos kelmasa tuzatish talab qilinadi. |

Lokal agent yordamida test: quote javobidan keyin, commitdan oldin balans yoki qoldiqni o‘zgartiring. Yakuniy tranzaksiya noto‘g‘ri miqdor/pulni qabul qilmasin; rad etilgan so‘rovdan tiklanish ishlasin. Shuningdek, commitdan keyin javob/natija saqlanishini uzib, qayta oching: oldingi request o‘zgargan quote sabab bloklanmasin va ikkinchi moliyaviy yozuv yaratilmasin.

Natijada test №, commit, kutilgan/haqiqiy qiymat va xato matnini qayd qiling. 3-bosqich — foyda qoidasi; 4-bosqich — IP orqali avtomatik topish hali qolgan.

# 2026-10-13: mobil qaytarish, tuzatishlarning 1-bosqichi

Branch: `fix/debt-stage3-complete`. Boshlang‘ich commit: `e0be726`.

## Bajarilgan ish

- Nasiya qaytarish oynasida hali miqdor tanlanmagan `0` holati preview sifatida ko‘rsatiladi; `splitReturn(0, ...)` chaqirilmaydi. Hisoblash xatosi ekran chizilishini to‘xtatmaydi.
- `10000`, `10000.0`, `10000.00` pul qiymatlari son sifatida teng tekshiriladi.
- Bo‘sh matn, manfiy son, harf yoki noto‘g‘ri format avtomatik nolga aylantirilmaydi. Pul summasida ko‘pi bilan ikki mazmunli kasr xona qabul qilinadi; vergul ham ishlaydi.
- Lokal tarixiy hisob, oldingi qaytarishlar va qarz balansi o‘qilmasa, taxminiy narx/nol qo‘yilmaydi. Serverdan tekshirilgan ma’lumot olish mumkin; ikkala manba ham yaroqsiz bo‘lsa yangi qaytarish tasdiqlanmaydi.
- Lokal qaytarish/qarz ma’lumoti bir baza tranzaksiyasida o‘qiladi.
- Saqlangan so‘rov o‘qilmasa yangi so‘rov bilan ustidan yozishga ruxsat berilmaydi. Oldin yuborilgan so‘rov natijasini tekshirish previewga bog‘lanmaydi.
- Coroutine bekor qilinishi yutilmaydi. Qaytarish va qaytarishni bekor qilish oynalaridagi draft o‘qish/xato tiklash yo‘llari himoyalandi.

UI joylashuvi o‘zgarmadi. Desktop, foyda hisoblash va IP topish tuzatishlari keyingi 2–4-bosqichlarda. Build, CI, emulator yoki avtomatik test bajarilmadi; APK yaratilmagan. Faqat o‘zgargan kod va diff ko‘rib chiqildi.

## Manual test

Avval test bazasidan foydalaning. Mavjud checkoutda saqlanmagan o‘zgarish bo‘lsa, uni almashtirmang. Toza alohida papkaga:

```powershell
git clone --branch fix/debt-stage3-complete --single-branch https://github.com/akkiS18/LinePOS.git LinePOS-mobile-return-test
cd LinePOS-mobile-return-test
git log -1 --oneline
```

Mobil ilovani shu koddan build qiling. Desktopga bu bosqichda o‘zgarish kiritilmadi; avvalgi shu test branchidagi desktop ishlatiladi.

| Test | Amal | Kutiladigan natija |
|---|---|---|
| 1 | Mobilda to‘liq nasiya chekining qaytarish oynasini oching; miqdorlarni 0 qoldiring. | Ilova yopilmaydi. Preview 0; yangi qaytarish tasdiqlanmaydi. |
| 2 | Qisman to‘langan nasiya va oddiy naqd chek uchun ham oynani oching. | Nasiya bo‘linishi to‘g‘ri; oddiy chek qarz sifatida talqin qilinmaydi. |
| 3 | Testdagi 21 000lik savdo, 11 000 qarz qoldig‘ida hammasini qaytaring. Naqdga qo‘lda 10000, kartaga 0 yozing. | 11 000 qarzdan chegirish, 10 000 pul qaytarish. Kasr formati sababli rad etilmaydi. |
| 4 | Alohida cheklarda 10000.0, 10000.00 va 10000,00 kiriting. | Bir xil pul qiymati sifatida qabul qilinadi. Bitta real qaytarishni uch marta bajarmang. |
| 5 | Miqdor/pul maydonini tozalang, harf, -1, ikki nuqta yoki pulga 1.001 kiriting. | Crash yo‘q; qiymat nolga aylanmaydi; noto‘g‘ri amal saqlanmaydi. To‘g‘ri qiymatga almashtirgach davom etish mumkin. |
| 6 | Miqdorni mavjudidan ortiq kiriting, keyin 0ga qaytaring. | Xabar chiqadi, ilova yopilmaydi, yangi qaytarish saqlanmaydi. |
| 7 | Wi-Fi uzilganida to‘liq lokal ma’lumotli chekni oching. | Lokal preview/draft ishlaydi; yakuniy tasdiq uchun aloqa kerakligi ko‘rsatiladi. |
| 8 | Draftni saqlab, oynani/ilovani qayta oching. | Miqdor, pul, sabab va request saqlanadi. |
| 9 | Qaytarish natijasi olingach “Tushunarli”ni bosing; qaytarishni bekor qilish oynasini ham oching. | Natija odatiy yopiladi, oynalar crash qilmaydi. |
| 10 | Oyna yuklanayotganida ilovani fon holatiga o‘tkazing/qayta oching; aloqa uzilishi bilan ham sinang. | Ilova crash qilmaydi. Jarayon o‘ldirilsa, keyingi ochilish saqlangan so‘rovni tiklaydi. |
| 11 | Katta summa (masalan, 10 000 000 so‘m) va kasr miqdorli mahsulot cheki. | Server JSON raqamlarining formati hisobni buzmaydi; oddiy formatda preview chiqadi. |

Lokal agent uchun qo‘shimcha nosozlik ssenariysi: faqat test nusxasida qaytarish ma’lumoti/draft o‘qishini xatoga uchrating. Lokal o‘qish xatosida to‘g‘ri server quote bilan ishlash mumkin. Ikkala quote ham olinmasa yangi qaytarish bloklanadi. Draft o‘qilmasa yangi request yaratilmaydi; crash o‘rniga xabar ko‘rinadi. Haqiqiy mijoz bazasida jadval yoki tarixni o‘chirmang.

Natijada test raqami, haqiqiy xabar va build commitini yozing. 2-bosqichdagi balans o‘zgarganida qayta hisoblash, 3-bosqich foyda va 4-bosqich IP almashishi hali tugallanmagan. Main merge yo‘q.

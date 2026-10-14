# Qaytarish tasdig‘i va foyda yorliqlari

## O‘zgarish

- Mobil/desktop yangi qaytarish oldidan fresh quote oladi va qisqa tasdiq chiqaradi: Mijozga qaytariladi; faqat >0 bo‘lsa Qarzdan chegiriladi. Karta qismi mavjud bo‘lsa naqd/karta ajratiladi. Yo‘q / Ha, qaytarish.
- Hisob yangilanishi avvalgi uzun ogohlantirish va yana asosiy tugmani bosishni talab qilmaydi. Tasdiqdagi summalar uchun immutable request saqlanadi. Noma’lum natijali submitted request o‘sha ID bilan replay qilinadi.
- Tasdiq ochiq paytda boshqa qurilma ma’lumotni o‘zgartirsa serverning atomik validationi saqlangan; xatoli yangi summa yashirin yuborilmaydi. Bunday poyga holatida qayta tasdiq kerak bo‘lishi mumkin.
- Muvaffaqiyatli qaytarishdan keyin qaytarish va chek tafsilotlari yopiladi. Mobil durable result ACK muvaffaqiyatsiz bo‘lsa natijani qayta ko‘rish imkoniyati saqlanadi.
- Qaytarish/bekor qilish tafsilotida Foydaga ta’siri; desktop jadvalda Foyda / ta’siri; Excelda Foyda / ta’sir (so‘m), ikki qatorli sarlavha. Hisoblash va jami summalar o‘zgartirilmadi.
- Qarzlar jadvalidagi Holati katagida gorizontal cheksiz StackPanel o‘rniga mavjud kenglikka mos Grid va Wrap: Faol nasiya ikki qatorga tushishi mumkin. Ustun kengligi o‘zgarmadi.

## Lokal tekshirish

1. Android compileDebugKotlin va desktop buildni bajaring. Bu muhitda build/runtime tekshiruvlari bajarilmadi. XAML XML parsing va diff check o‘tdi; render testi emas.
2. Mobil va desktopda 3 dona chekdan 1 dona qaytaring. Qisqa tasdiq bir marta chiqsin. Yo‘q tanlansa bazaga qaytarish yozilmasin, forma saqlansin. Ha tanlansa bir marta yozilsin va ikki oyna yopilsin.
3. Nasiya balansi 62000, qaytarish 40000: Mijozga 0, Qarzdan 40000. Qaytarish 82000 bo‘lsa: Mijozga 20000, Qarzdan 62000. Oddiy naqd chekda qarz satri bo‘lmasin.
4. Panel ochiq paytda boshqa qurilmada to‘lov qiling. Asosiy qaytarish tugmasida yangilangan raqamlar bilan tasdiq chiqsin. Tasdiq ochiq paytda yana to‘lov qilinsa server noto‘g‘ri summani qabul qilmasin.
5. Ha tugmasini tez ikki marta bosing; javob yo‘qolishi/restart holatida request replay sinang. Dublikat to‘lov/qoldiq bo‘lmasin.
6. Musbat va manfiy return effect tafsilotda to‘g‘ri belgi bilan ko‘rinsin. Umumiy hisobot va Excel jami o‘zgarmasin; foyda qatorlari hali hisobga olinsin.
7. Desktop 100%,125%,150% scaling va kichik oynada Holati, foyda sarlavhasi, chek tafsilotini ko‘ring. Faol nasiya va boshqa holatlar qirqilmasin. Ustunlar kengayib qo‘shni ustunlarni siqmasin. Excelni ochib ikki qatorli sarlavhani tekshiring.
8. Android kichik ekran/kattalashtirilgan shriftda tasdiq va foyda yorlig‘ini tekshiring.

UI talabi shu o‘zgarishlar doirasida bajarildi; Firebase/CBU, moliyaviy projection va sync protokoliga o‘zgartirish yo‘q. Main merge yo‘q.

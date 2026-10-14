# Qarz pull himoyasi — 2026-10-14

494949f tekshiruvidan keyingi tuzatishlar:

- Envelope yo‘q bo‘lsa, faqat group GUIDiga mos mijoz aynan shu HTTP javobidagi customers ro‘yxatida bo‘lsa, groupda faqat shu mijozning debt_customer yozuvlari bo‘lsa va group GUIDiga mos debt_event bo‘lmasa kursor undan o‘tadi.
- Moliyaviy yoki noma’lum group uchun envelope yo‘q bo‘lsa so‘rov xato qaytaradi, muvaffaqiyatli kursor berilmaydi. Noto‘g‘ri GUIDli guruh ham jim o‘tkazilmaydi.
- returns/return_items yaratish xatosi endi yutilmaydi: sabab inner exception bilan saqlanib, baza ochilishi to‘xtaydi.
- LAN testga haqiqiy payment envelope sealini vaqtincha olib tashlash, full/oddiy pull xato va kursorsiz javobini tekshirish, qayta tiklangach payment yetkazilishini tekshirish qo‘shildi.
- Repository testga archived mijozning YANGI to‘lovi balansni 3000dan 2000ga kamaytirishi, qayta yuborish qo‘shimcha event/line/sale yaratmasligi va yangi nasiya taqiqlanishi qo‘shildi.

## Lokal tekshirish

LinePOS desktopni odatiy yopib, 8080 port bo‘shligini tekshiring. Saqlanmagan ish bilan jarayonni force-kill qilmang.

```powershell
dotnet run --project tests/WifiSync.CoreTests
dotnet run --project tests/Business.CoreTests
dotnet build desktop/PosElectro.Desktop/PosElectro.Desktop.csproj
```

Testlar o‘z vaqtinchalik bazalarida ishlaydi. Haqiqiy bazadan envelope o‘chirmang.

Holat: kod review va git diff --check bajarildi. C# build/runtime testlar cloud muhitidagi avval aniqlangan .NET cheklovi sabab bajarilmadi; yangi testlar PASS deb tasdiqlanmagan. Lokal agent hisobotidagi oldingi PASS bu yangi commitga avtomatik tatbiq etilmaydi. UI/Firebase/CBU va foydalanuvchi zaxiralari o‘zgartirilmadi. Main merge qilinmadi.

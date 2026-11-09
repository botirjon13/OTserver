# Google Drive zaxiralash sozlamasi

## OAuth sozlamasi

Google Cloud Console'da OAuth 2.0 Client ID yarating va `Web application` turini tanlang. `Desktop app` credential server integratsiyasi uchun ishlatilmaydi.

Authorized redirect URI:

```text
https://otserver.onrender.com/admin/google-drive/oauth/callback
```

Serverga quyidagi environment variable'larni kiriting:

```text
GOOGLE_DRIVE_CLIENT_ID=<Web application client ID>
GOOGLE_DRIVE_CLIENT_SECRET=<Web application client secret>
GOOGLE_DRIVE_REDIRECT_URI=https://otserver.onrender.com/admin/google-drive/oauth/callback
```

Client secret'ni repository'ga yoki desktop ilovaga qo'shmang. OAuth sozlamalari kiritilgach, SuperAdmin sifatida `/admin/google-drive` sahifasini ochib Google hisobini ulang.

## Ma'lumotlarni himoyalash va saqlash siyosati

- Desktop ilova foydalanuvchidan alohida rozilik olmaguncha Drive'ga zaxira yubormaydi.
- Ma'lumotlar bazasi va mahsulot rasmlari Drive'ga yuborilishidan oldin desktop ilovada AES-256-GCM bilan shifrlanadi.
- Ilova zaxirani oyiga bir marta avtomatik yuboradi; foydalanuvchi qo'lda ham yuborishi mumkin.
- Har bir litsenziya/qurilma uchun yangi fayl Drive'dan qayta yuklab olinib yaxlitligi tasdiqlangandan keyin eski fayllar o'chiriladi.
- Zaxira fayllari Google Drive hisobining asosiy papkasida saqlanadi.
- Tiklash kaliti yangi kompyuterda zaxirani ochish uchun zarur. Uni xavfsiz joyda saqlang; kalit yo'qolsa, shifrlangan nusxani tiklab bo'lmaydi.
- Refresh token server bazasida mavjud `LICENSE_SIGNING_KEY` va `ADMIN_RECOVERY_KEY` konfiguratsiyasidan hosil qilingan kalit bilan shifrlanib saqlanadi. Ushbu server kalitlari o'zgarsa Google hisobini qayta ulash talab qilinishi mumkin.

Google OAuth consent screen `Testing` rejimida bo'lsa, refresh token muddati cheklanishi mumkin. Ishlab chiqarishda consent screen publishing status va Google talablari bilan tanishing.

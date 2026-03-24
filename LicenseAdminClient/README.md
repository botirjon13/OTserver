# LicenseAdminClient

Bu alohida `AdminClient.exe` bo'lib, Railway/VPS dagi `LicenseAdminServer` API bilan ishlaydi.

## Ishga tushirish

```powershell
dotnet run --project LicenseAdminClient\LicenseAdminClient.csproj
```

## Foydalanish

1. `Server URL` kiriting (masalan: `https://your-app.up.railway.app`)
2. `Admin login/parol` kiriting
3. `Ulanish` tugmasini bosing
4. Dashboard, Licenses, Devices bo'limlaridan boshqaring

## API auth

Client `Basic Auth` orqali `/api/admin/*` endpointlarga ulanadi.

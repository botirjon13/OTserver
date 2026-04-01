using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRouting();

var app = builder.Build();

string? port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

const string AdminSessionCookieName = "license_admin_session";
const string CsrfCookieName = "license_admin_csrf";
var webSessions = new ConcurrentDictionary<string, AdminAuth>(StringComparer.Ordinal);
var activationAttempts = new ConcurrentDictionary<string, (int Count, DateTime WindowStartUtc)>(StringComparer.Ordinal);
string adminUser = Environment.GetEnvironmentVariable("ADMIN_USERNAME")?.Trim() ?? "admin";
string adminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD")?.Trim() ?? "change-me";
string signingKey = Environment.GetEnvironmentVariable("LICENSE_SIGNING_KEY")?.Trim() ?? "change-this-signing-key";
string recoveryKey = Environment.GetEnvironmentVariable("ADMIN_RECOVERY_KEY")?.Trim() ?? "change-this-recovery-key";
if (IsProductionLikeEnvironment() &&
    (string.Equals(adminPass, "change-me", StringComparison.Ordinal) ||
     string.Equals(signingKey, "change-this-signing-key", StringComparison.Ordinal) ||
     string.Equals(recoveryKey, "change-this-recovery-key", StringComparison.Ordinal)))
{
    throw new InvalidOperationException("Xavfsizlik xatosi: production muhitida default secretlardan foydalanish mumkin emas. ADMIN_PASSWORD, LICENSE_SIGNING_KEY, ADMIN_RECOVERY_KEY ni o'zgartiring.");
}
string? rawDbConnection = Environment.GetEnvironmentVariable("DATABASE_URL")?.Trim();
if (string.IsNullOrWhiteSpace(rawDbConnection))
{
    rawDbConnection = Environment.GetEnvironmentVariable("LICENSE_DB_PATH")?.Trim();
}
if (string.IsNullOrWhiteSpace(rawDbConnection))
{
    throw new InvalidOperationException("DATABASE_URL topilmadi. Railway PostgreSQL ulanish satrini env sifatida kiriting.");
}
string dbPath = NormalizePostgresConnectionString(rawDbConnection);
string? appDataDir = Environment.GetEnvironmentVariable("APP_DATA_DIR")?.Trim();
if (string.IsNullOrWhiteSpace(appDataDir))
{
    appDataDir = Directory.Exists("/data")
        ? "/data"
        : Path.Combine(app.Environment.ContentRootPath, "App_Data");
}
Directory.CreateDirectory(appDataDir);
string backupDir = Path.Combine(appDataDir, "backups");
Directory.CreateDirectory(backupDir);
string defaultSupportContact = Environment.GetEnvironmentVariable("DEFAULT_SUPPORT_CONTACT")?.Trim()
    ?? "Savdo bo'limi: +998 90 000 00 00 | Telegram: @your_support";
string defaultFirstLoginUsername = Environment.GetEnvironmentVariable("CLIENT_BOOTSTRAP_USERNAME")?.Trim()
    ?? "admin";
string releaseProxyBaseUrl = NormalizeReleaseProxyBaseUrl(
    Environment.GetEnvironmentVariable("RELEASE_FEED_SOURCE_URL")?.Trim()
    ?? "https://raw.githubusercontent.com/botirjon13/OTserver/main/releases");
var releaseProxyHttpClient = new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
})
{
    Timeout = TimeSpan.FromMinutes(5)
};
releaseProxyHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Osontrack-LicenseAdminServer/1.0");
Db.Initialize(dbPath);
Db.ConfigureSecrets(signingKey, recoveryKey);
string sqliteMigrationPath = Environment.GetEnvironmentVariable("SQLITE_MIGRATION_PATH")?.Trim()
    ?? (Directory.Exists("/data") ? Path.Combine("/data", "license_admin.db") : string.Empty);
string migrationState = Db.TryMigrateFromSqlite(dbPath, sqliteMigrationPath);
if (!string.Equals(migrationState, "skipped", StringComparison.Ordinal))
{
    Console.WriteLine($"[MIGRATION] {migrationState}");
}
Db.EnsureDefaultAdmin(dbPath, adminUser, adminPass);
Db.EnsureClientDefaults(dbPath, defaultSupportContact, defaultFirstLoginUsername);

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/admin", out var remaining))
    {
        bool isLoginPath = remaining.Equals("/login", StringComparison.OrdinalIgnoreCase);
        bool authenticated = TryRequireWebAdmin(context, webSessions, out _, out _);
        if (!authenticated && !isLoginPath)
        {
            context.Response.Redirect("/admin/login");
            return;
        }
        if (authenticated && isLoginPath && HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Redirect("/admin");
            return;
        }
    }

    await next();
});

app.MapGet("/", () => Results.Redirect("/admin"));

app.MapGet("/admin/login", (HttpContext context) =>
{
    string csrf = EnsureCsrfToken(context);
    string html = """
<!doctype html>
<html><head><meta charset="utf-8"/><title>Admin Login</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:32px}
.card{max-width:420px;margin:40px auto;background:#fff;border-radius:14px;padding:24px;box-shadow:0 8px 24px rgba(32,56,85,.12)}
h2{margin:0 0 18px;color:#233a5a} label{display:block;font-size:13px;color:#4a607f;margin:10px 0 6px}
input{width:100%;padding:10px 12px;border:1px solid #cad6e6;border-radius:10px;font-size:14px}
button{margin-top:14px;width:100%;padding:10px;border:none;border-radius:10px;background:#2f6fd8;color:#fff;font-weight:700;cursor:pointer}
p{font-size:12px;color:#6f84a2}
</style></head>
<body><div class="card">
<h2>License Admin Login</h2>
<form method="post" action="/admin/login">
<input type="hidden" name="csrf" value="__CSRF__" />
<label>Login</label><input name="username" required />
<label>Parol</label><input type="password" name="password" required />
<button type="submit">Kirish</button>
</form>
<p>Default: admin / change-me (env orqali almashtiring)</p>
</div></body></html>
""";
    html = html.Replace("__CSRF__", HttpUtility.HtmlEncode(csrf));
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/admin/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/login'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string username = form["username"].ToString().Trim();
    string password = form["password"].ToString();
    var auth = Db.AuthenticateAdmin(dbPath, username, password, adminUser, adminPass);
    if (auth == null)
    {
        return Results.Content("<h3>Login yoki parol noto'g'ri. <a href='/admin/login'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string sessionId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    webSessions[sessionId] = auth;
    context.Response.Cookies.Append(AdminSessionCookieName, sessionId, new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps
    });
    return Results.Redirect("/admin");
});

app.MapPost("/admin/logout", async (HttpContext context) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out _, out _))
    {
        return Results.Redirect("/admin/login");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string sid = context.Request.Cookies[AdminSessionCookieName] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(sid))
    {
        webSessions.TryRemove(sid, out _);
    }
    context.Response.Cookies.Delete(AdminSessionCookieName);
    context.Response.Cookies.Delete(CsrfCookieName);
    return Results.Redirect("/admin/login");
});

app.MapGet("/admin", (HttpContext context) =>
{
    string csrf = EnsureCsrfToken(context);
    var stats = Db.GetDashboardStats(dbPath);
    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>License Admin</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.top{display:flex;justify-content:space-between;align-items:center;max-width:1050px;margin:0 auto 16px}
.btn{border:none;border-radius:10px;padding:8px 12px;font-weight:700;cursor:pointer}
.logout{background:#eb5757;color:#fff}
.wrap{max-width:1050px;margin:0 auto}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}
.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08)}
.k{font-size:12px;color:#6f84a2}.v{font-size:24px;font-weight:800;margin-top:6px}
.actions{display:flex;gap:10px;margin-top:14px}
a.x{display:inline-block;background:#2f6fd8;color:#fff;text-decoration:none;padding:10px 14px;border-radius:10px;font-weight:700}
a.y{display:inline-block;background:#0f9d58;color:#fff;text-decoration:none;padding:10px 14px;border-radius:10px;font-weight:700}
a.z{display:inline-block;background:#8e44ad;color:#fff;text-decoration:none;padding:10px 14px;border-radius:10px;font-weight:700}
a.w{display:inline-block;background:#e67e22;color:#fff;text-decoration:none;padding:10px 14px;border-radius:10px;font-weight:700}
</style></head>
<body>
<div class="top"><h2>License Admin Panel</h2>
<form method="post" action="/admin/logout"><input type="hidden" name="csrf" value="{{HttpUtility.HtmlEncode(csrf)}}" /><button class="btn logout" type="submit">Chiqish</button></form></div>
<div class="wrap">
<div class="grid">
<div class="card"><div class="k">Jami license</div><div class="v">{{stats.TotalLicenses}}</div></div>
<div class="card"><div class="k">Aktiv license</div><div class="v">{{stats.ActiveLicenses}}</div></div>
<div class="card"><div class="k">Jami qurilma</div><div class="v">{{stats.TotalDevices}}</div></div>
<div class="card"><div class="k">Bugun online</div><div class="v">{{stats.ActiveToday}}</div></div>
</div>
<div class="actions">
<a class="x" href="/admin/licenses">License boshqarish</a>
<a class="y" href="/admin/devices">Qurilmalar ro'yxati</a>
<a class="z" href="/admin/clients">Mijozlar</a>
<a class="w" href="/admin/settings">Sozlamalar</a>
<a class="x" href="/admin/audit">Audit</a>
</div>
</div>
</body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/admin/licenses", (HttpContext context) =>
{
    string csrf = EnsureCsrfToken(context);
    var rows = Db.GetLicenses(dbPath);
    string items = string.Join("", rows.Select(r =>
        $"<tr><td>{r.Id}</td><td>{HttpUtility.HtmlEncode(r.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(r.CustomerName)}</td><td>{r.MaxDevices}</td><td>{HttpUtility.HtmlEncode(r.Status)}</td><td>{HttpUtility.HtmlEncode(r.ExpiresAt ?? "-")}</td><td><form method='post' action='/admin/licenses/{r.Id}/toggle'><input type='hidden' name='csrf' value='{HttpUtility.HtmlEncode(csrf)}' /><button type='submit'>{(r.Status == "Active" ? "Block" : "Activate")}</button></form></td></tr>"));

    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>Licenses</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.wrap{max-width:1100px;margin:0 auto}.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08);margin-bottom:12px}
table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #e4ebf5;padding:8px;font-size:13px;text-align:left}
input{padding:8px;border:1px solid #cad6e6;border-radius:8px}
button{padding:8px 12px;border:none;background:#2f6fd8;color:#fff;border-radius:8px;cursor:pointer}
a{color:#2f6fd8;text-decoration:none;font-weight:700}
</style></head>
<body><div class="wrap">
<p><a href="/admin">← Dashboard</a></p>
<div class="card">
<h3>Yangi license yaratish</h3>
<form method="post" action="/admin/licenses/create">
<input type="hidden" name="csrf" value="{{HttpUtility.HtmlEncode(csrf)}}" />
<input name="customerName" placeholder="Mijoz nomi" required />
<input name="maxDevices" type="number" min="1" value="1" required />
<input name="expiresAt" placeholder="YYYY-MM-DD (ixtiyoriy)" />
<button type="submit">Yaratish</button>
</form>
</div>
<div class="card">
<h3>License ro'yxati</h3>
<table>
<thead><tr><th>ID</th><th>Key</th><th>Mijoz</th><th>Qurilma limiti</th><th>Holat</th><th>Muddat</th><th>Amal</th></tr></thead>
<tbody>{{items}}</tbody>
</table>
</div></div></body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/admin/licenses/create", async (HttpContext context) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out AdminAuth? auth, out _))
    {
        return Results.Redirect("/admin/login");
    }
    if (!string.Equals(auth!.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content("<h3>Ruxsat yo'q (faqat SuperAdmin). <a href='/admin/licenses'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/licenses'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string customerName = form["customerName"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(customerName))
    {
        customerName = "No name";
    }

    int maxDevices = int.TryParse(form["maxDevices"], out int md) && md > 0 ? md : 1;
    string expiresAtRaw = form["expiresAt"].ToString().Trim();
    string? expiresAt = null;
    if (!string.IsNullOrWhiteSpace(expiresAtRaw) &&
        DateTime.TryParseExact(expiresAtRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
    {
        expiresAt = dt.ToString("yyyy-MM-dd");
    }

    Db.CreateLicense(dbPath, customerName, maxDevices, expiresAt);
    return Results.Redirect("/admin/licenses");
});

app.MapPost("/admin/licenses/{id:int}/toggle", async (HttpContext context, int id) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out AdminAuth? auth, out _))
    {
        return Results.Redirect("/admin/login");
    }
    if (!string.Equals(auth!.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content("<h3>Ruxsat yo'q (faqat SuperAdmin). <a href='/admin/licenses'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/licenses'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    Db.ToggleLicense(dbPath, id);
    return Results.Redirect("/admin/licenses");
});

app.MapGet("/admin/devices", () =>
{
    var rows = Db.GetDevices(dbPath);
    string items = string.Join("", rows.Select(r =>
        $"<tr><td>{r.Id}</td><td>{HttpUtility.HtmlEncode(r.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(r.DeviceId)}</td><td>{HttpUtility.HtmlEncode(r.DeviceName)}</td><td>{HttpUtility.HtmlEncode(r.AppVersion)}</td><td>{HttpUtility.HtmlEncode(r.FirstSeenAt)}</td><td>{HttpUtility.HtmlEncode(r.LastSeenAt)}</td></tr>"));

    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>Devices</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.wrap{max-width:1100px;margin:0 auto}.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08)}
table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #e4ebf5;padding:8px;font-size:13px;text-align:left}
a{color:#2f6fd8;text-decoration:none;font-weight:700}
</style></head>
<body><div class="wrap">
<p><a href="/admin">← Dashboard</a></p>
<div class="card">
<h3>Qurilmalar</h3>
<table><thead><tr><th>ID</th><th>License</th><th>Device ID</th><th>Device nomi</th><th>Versiya</th><th>Birinchi ko'rilgan</th><th>Oxirgi ko'rilgan</th></tr></thead>
<tbody>{{items}}</tbody></table>
</div></div></body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/admin/clients", (HttpContext context) =>
{
    string csrf = EnsureCsrfToken(context);
    string licenseKey = context.Request.Query["licenseKey"].ToString().Trim();
    string deviceId = context.Request.Query["deviceId"].ToString().Trim();

    var telemetry = Db.GetClientUserTelemetry(dbPath, licenseKey, deviceId, 100);
    var firstLogins = Db.GetClientFirstLoginCredentials(dbPath, licenseKey, deviceId, 100);
    var resets = Db.GetClientPasswordResets(dbPath, licenseKey, deviceId, null, null, 100);
    var backups = Db.GetClientBackupRows(dbPath, licenseKey, deviceId, 100);

    string telemetryRows = string.Join("", telemetry.Select(t =>
        $"<tr><td>{t.Id}</td><td>{HttpUtility.HtmlEncode(t.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(t.DeviceId)}</td><td>{t.UserCount}</td><td>{HttpUtility.HtmlEncode(t.UsernamesJson)}</td><td>{HttpUtility.HtmlEncode(t.CreatedAt)}</td></tr>"));
    string firstRows = string.Join("", firstLogins.Select(f =>
        $"<tr><td>{f.Id}</td><td>{HttpUtility.HtmlEncode(f.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(f.DeviceId)}</td><td>{HttpUtility.HtmlEncode(f.Username)}</td><td>{HttpUtility.HtmlEncode(MaskSecret(f.TempPassword))}</td><td>{HttpUtility.HtmlEncode(f.Status)}</td><td>{HttpUtility.HtmlEncode(f.CreatedAt)}</td><td>{HttpUtility.HtmlEncode(f.AppliedAt ?? "-")}</td></tr>"));
    string resetRows = string.Join("", resets.Select(r =>
        $"<tr><td>{r.Id}</td><td>{HttpUtility.HtmlEncode(r.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(r.DeviceId ?? "-")}</td><td>{HttpUtility.HtmlEncode(r.Username)}</td><td>{HttpUtility.HtmlEncode(MaskSecret(r.TempPassword))}</td><td>{HttpUtility.HtmlEncode(r.Status)}</td><td>{HttpUtility.HtmlEncode(r.CreatedAt)}</td><td>{HttpUtility.HtmlEncode(r.AppliedAt ?? "-")}</td></tr>"));
    string backupRows = string.Join("", backups.Select(b =>
        $"<tr><td>{b.Id}</td><td>{HttpUtility.HtmlEncode(b.LicenseKey)}</td><td>{HttpUtility.HtmlEncode(b.DeviceId)}</td><td>{HttpUtility.HtmlEncode(b.FileName)}</td><td>{b.SizeBytes}</td><td>{HttpUtility.HtmlEncode(b.CreatedAt)}</td><td><a href='/admin/clients/backups/{b.Id}/download'>Yuklab olish</a></td></tr>"));

    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>Mijozlar</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.wrap{max-width:1200px;margin:0 auto}.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08);margin-bottom:12px}
table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #e4ebf5;padding:8px;font-size:13px;text-align:left;vertical-align:top}
input{padding:8px;border:1px solid #cad6e6;border-radius:8px}
button{padding:8px 12px;border:none;background:#2f6fd8;color:#fff;border-radius:8px;cursor:pointer}
a{color:#2f6fd8;text-decoration:none;font-weight:700}
.row{display:flex;gap:8px;flex-wrap:wrap}
</style></head>
<body><div class="wrap">
<p><a href="/admin">← Dashboard</a></p>
<div class="card">
<h3>Filter</h3>
<form method="get" action="/admin/clients" class="row">
<input name="licenseKey" placeholder="License key" value="{{HttpUtility.HtmlEncode(licenseKey)}}" />
<input name="deviceId" placeholder="Device ID" value="{{HttpUtility.HtmlEncode(deviceId)}}" />
<button type="submit">Qidirish</button>
</form>
</div>
<div class="card">
<h3>Mijoz parolini masofadan tiklash</h3>
<form method="post" action="/admin/clients/password-resets/create" class="row">
<input type="hidden" name="csrf" value="{{HttpUtility.HtmlEncode(csrf)}}" />
<input name="licenseKey" placeholder="License key" required />
<input name="deviceId" placeholder="Device ID (ixtiyoriy)" />
<input name="username" placeholder="Username" required />
<input name="tempPassword" placeholder="Temporary parol" required />
<button type="submit">Reset yuborish</button>
</form>
</div>
<div class="card"><h3>Birinchi kirish login/parol</h3>
<table><thead><tr><th>ID</th><th>License</th><th>Device</th><th>Username</th><th>Parol</th><th>Status</th><th>Created</th><th>Applied</th></tr></thead><tbody>{{firstRows}}</tbody></table>
</div>
<div class="card"><h3>Password resetlar tarixi</h3>
<table><thead><tr><th>ID</th><th>License</th><th>Device</th><th>Username</th><th>Temp parol</th><th>Status</th><th>Created</th><th>Applied</th></tr></thead><tbody>{{resetRows}}</tbody></table>
</div>
<div class="card"><h3>Mijoz user telemetry</h3>
<table><thead><tr><th>ID</th><th>License</th><th>Device</th><th>User count</th><th>Usernames</th><th>Created</th></tr></thead><tbody>{{telemetryRows}}</tbody></table>
</div>
<div class="card"><h3>Mijoz backup fayllari</h3>
<table><thead><tr><th>ID</th><th>License</th><th>Device</th><th>File</th><th>Size</th><th>Created</th><th>Amal</th></tr></thead><tbody>{{backupRows}}</tbody></table>
</div>
</div></body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/admin/clients/password-resets/create", async (HttpContext context) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out AdminAuth? auth, out _))
    {
        return Results.Redirect("/admin/login");
    }
    if (!string.Equals(auth!.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content("<h3>Ruxsat yo'q (faqat SuperAdmin). <a href='/admin/clients'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/clients'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string licenseKey = form["licenseKey"].ToString().Trim();
    string deviceId = form["deviceId"].ToString().Trim();
    string username = form["username"].ToString().Trim();
    string tempPassword = form["tempPassword"].ToString();
    if (!string.IsNullOrWhiteSpace(licenseKey) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(tempPassword))
    {
        int id = Db.CreateClientPasswordReset(dbPath, licenseKey, string.IsNullOrWhiteSpace(deviceId) ? null : deviceId, username, tempPassword);
        Db.AddAuditLog(dbPath, "CLIENT_PASSWORD_RESET_CREATE_WEB", $"Web orqali reset: id={id}, key={licenseKey}, device={deviceId}, username={username}");
    }

    return Results.Redirect("/admin/clients");
});

app.MapGet("/admin/clients/backups/{id:int}/download", (int id) =>
{
    ClientBackupDownloadRow? row = Db.GetClientBackupDownloadById(dbPath, id);
    if (row == null)
    {
        return Results.NotFound("<h3>Backup topilmadi. <a href='/admin/clients'>Qaytish</a></h3>");
    }

    string fileName = $"{row.LicenseKey}_{row.FileName}";
    if (row.FileData != null && row.FileData.Length > 0)
    {
        return Results.File(row.FileData, "application/octet-stream", fileName);
    }
    if (!string.IsNullOrWhiteSpace(row.StoredPath) && File.Exists(row.StoredPath))
    {
        return Results.File(row.StoredPath, "application/octet-stream", fileName);
    }
    return Results.NotFound("<h3>Backup fayli topilmadi. <a href='/admin/clients'>Qaytish</a></h3>");
});

app.MapGet("/admin/settings", (HttpContext context) =>
{
    string csrf = EnsureCsrfToken(context);
    var cfg = Db.GetAppVersionConfig(dbPath);
    string contact = Db.GetSupportContactInfo(dbPath);
    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>Sozlamalar</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.wrap{max-width:1100px;margin:0 auto}.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08);margin-bottom:12px}
input,textarea{width:100%;padding:8px;border:1px solid #cad6e6;border-radius:8px}
button{padding:8px 12px;border:none;background:#2f6fd8;color:#fff;border-radius:8px;cursor:pointer;font-weight:700}
label{display:block;margin:8px 0 6px;font-size:13px;color:#5f7698}
a{color:#2f6fd8;text-decoration:none;font-weight:700}
</style></head>
<body><div class="wrap">
<p><a href="/admin">← Dashboard</a></p>
<div class="card">
<h3>Update version boshqaruvi</h3>
<form method="post" action="/admin/settings/version">
<input type="hidden" name="csrf" value="{{HttpUtility.HtmlEncode(csrf)}}" />
<label>Version</label><input name="version" value="{{HttpUtility.HtmlEncode(cfg.Version)}}" required />
<label>Update URL</label><input name="url" value="{{HttpUtility.HtmlEncode(cfg.Url)}}" />
<label>Izoh</label><textarea name="note" rows="3">{{HttpUtility.HtmlEncode(cfg.Note)}}</textarea>
<label><input type="checkbox" name="mandatory" value="1" {{(cfg.Mandatory ? "checked" : "")}} /> Majburiy update</label>
<button type="submit">Version saqlash</button>
</form>
</div>
<div class="card">
<h3>Mijoz bilan aloqa ma'lumoti</h3>
<form method="post" action="/admin/settings/contact">
<input type="hidden" name="csrf" value="{{HttpUtility.HtmlEncode(csrf)}}" />
<label>Kontakt matni</label>
<textarea name="contact" rows="3" required>{{HttpUtility.HtmlEncode(contact)}}</textarea>
<button type="submit">Aloqa ma'lumotini saqlash</button>
</form>
</div>
</div></body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/admin/settings/version", async (HttpContext context) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out AdminAuth? auth, out _))
    {
        return Results.Redirect("/admin/login");
    }
    if (!string.Equals(auth!.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content("<h3>Ruxsat yo'q (faqat SuperAdmin). <a href='/admin/settings'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/settings'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string version = form["version"].ToString().Trim();
    string url = form["url"].ToString().Trim();
    string note = form["note"].ToString().Trim();
    bool mandatory = string.Equals(form["mandatory"].ToString(), "1", StringComparison.Ordinal);
    if (!string.IsNullOrWhiteSpace(version))
    {
        Db.SetAppVersionConfig(dbPath, version, url, note, mandatory, string.Empty);
        Db.AddAuditLog(dbPath, "APP_VERSION_SET_WEB", $"Web orqali version saqlandi: {version}");
    }

    return Results.Redirect("/admin/settings");
});

app.MapPost("/admin/settings/contact", async (HttpContext context) =>
{
    if (!TryRequireWebAdmin(context, webSessions, out AdminAuth? auth, out _))
    {
        return Results.Redirect("/admin/login");
    }
    if (!string.Equals(auth!.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Content("<h3>Ruxsat yo'q (faqat SuperAdmin). <a href='/admin/settings'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    var form = await context.Request.ReadFormAsync();
    if (!ValidateCsrf(context, form))
    {
        return Results.Content("<h3>CSRF tekshiruvidan o'tmadi. <a href='/admin/settings'>Qaytish</a></h3>", "text/html; charset=utf-8");
    }

    string contact = form["contact"].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(contact))
    {
        Db.SetSupportContactInfo(dbPath, contact);
        Db.AddAuditLog(dbPath, "SUPPORT_CONTACT_SET_WEB", "Web orqali aloqa ma'lumoti yangilandi.");
    }

    return Results.Redirect("/admin/settings");
});

app.MapGet("/admin/audit", (HttpContext context) =>
{
    int limit = 200;
    if (int.TryParse(context.Request.Query["limit"], out int parsed) && parsed > 0 && parsed <= 2000)
    {
        limit = parsed;
    }

    var rows = Db.GetAuditLogs(dbPath, limit);
    string items = string.Join("", rows.Select(r =>
        $"<tr><td>{r.Id}</td><td>{HttpUtility.HtmlEncode(r.EventType)}</td><td>{HttpUtility.HtmlEncode(r.Message)}</td><td>{HttpUtility.HtmlEncode(r.CreatedAt)}</td></tr>"));
    string html = $$"""
<!doctype html>
<html><head><meta charset="utf-8"/><title>Audit</title>
<style>
body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:20px;color:#203656}
.wrap{max-width:1200px;margin:0 auto}.card{background:#fff;border-radius:12px;padding:14px;box-shadow:0 6px 18px rgba(33,59,95,.08)}
table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #e4ebf5;padding:8px;font-size:13px;text-align:left}
input{padding:8px;border:1px solid #cad6e6;border-radius:8px}
button{padding:8px 12px;border:none;background:#2f6fd8;color:#fff;border-radius:8px;cursor:pointer}
a{color:#2f6fd8;text-decoration:none;font-weight:700}
</style></head>
<body><div class="wrap">
<p><a href="/admin">← Dashboard</a></p>
<div class="card">
<h3>Audit loglar</h3>
<form method="get" action="/admin/audit" style="margin-bottom:10px">
<input type="number" name="limit" min="1" max="2000" value="{{limit}}" />
<button type="submit">Yangilash</button>
</form>
<table><thead><tr><th>ID</th><th>Event</th><th>Message</th><th>Created</th></tr></thead><tbody>{{items}}</tbody></table>
</div>
</div></body></html>
""";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/ping", () => Results.Json(new { ok = true, service = "license-admin" }));

app.MapGet("/api/admin/stats", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    var stats = Db.GetDashboardStats(dbPath);
    return Results.Json(new { ok = true, stats });
});

app.MapGet("/api/admin/licenses", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string search = context.Request.Query["q"].ToString();
    string status = context.Request.Query["status"].ToString();
    var rows = Db.GetLicenses(dbPath, search, status);
    return Results.Json(new { ok = true, items = rows });
});

app.MapPost("/api/admin/licenses/create", (HttpContext context, CreateLicenseApiRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string customerName = string.IsNullOrWhiteSpace(req.CustomerName) ? "No name" : req.CustomerName.Trim();
    int maxDevices = req.MaxDevices <= 0 ? 1 : req.MaxDevices;
    string? expiresAt = NormalizeDate(req.ExpiresAt);
    string key = Db.CreateLicense(dbPath, customerName, maxDevices, expiresAt);
    Db.AddAuditLog(dbPath, "LICENSE_CREATE", $"Yangi license yaratildi: {key}, customer={customerName}, maxDevices={maxDevices}, expiresAt={expiresAt ?? "-"}");
    Db.AddLicenseHistoryByKey(dbPath, key, "CREATE", $"License yaratildi. Mijoz={customerName}, limit={maxDevices}, muddat={expiresAt ?? "-"}");
    return Results.Json(new { ok = true, licenseKey = key });
});

app.MapPost("/api/admin/licenses/{id:int}/toggle", (HttpContext context, int id) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string? newStatus = Db.ToggleLicense(dbPath, id);
    if (!string.IsNullOrWhiteSpace(newStatus))
    {
        Db.AddAuditLog(dbPath, "LICENSE_TOGGLE", $"License #{id} holati o'zgardi: {newStatus}");
        Db.AddLicenseHistoryById(dbPath, id, "TOGGLE", $"Holat {newStatus} ga o'zgardi.");
    }

    return Results.Json(new { ok = true });
});

app.MapPut("/api/admin/licenses/{id:int}", (HttpContext context, int id, UpdateLicenseApiRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string customerName = string.IsNullOrWhiteSpace(req.CustomerName) ? "No name" : req.CustomerName.Trim();
    int maxDevices = req.MaxDevices <= 0 ? 1 : req.MaxDevices;
    string status = string.Equals(req.Status, "Blocked", StringComparison.OrdinalIgnoreCase) ? "Blocked" : "Active";
    string? expiresAt = NormalizeDate(req.ExpiresAt);

    var result = Db.UpdateLicense(dbPath, id, req.LicenseKey?.Trim() ?? string.Empty, customerName, maxDevices, status, expiresAt);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "LICENSE_UPDATE", $"License #{id} yangilandi: key={req.LicenseKey?.Trim()}, customer={customerName}, maxDevices={maxDevices}, status={status}, expiresAt={expiresAt ?? "-"}");
    Db.AddLicenseHistoryById(dbPath, id, "UPDATE", $"License yangilandi. Status={status}, limit={maxDevices}, muddat={expiresAt ?? "-"}");
    return Results.Json(new { ok = true });
});

app.MapDelete("/api/admin/licenses/{id:int}", (HttpContext context, int id) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    LicenseRow? before = Db.GetLicenseById(dbPath, id);
    var result = Db.DeleteLicense(dbPath, id);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "LICENSE_DELETE", $"License o'chirildi: #{id}, key={before?.LicenseKey ?? "-"}");
    if (before != null)
    {
        Db.AddLicenseHistoryByKey(dbPath, before.LicenseKey, "DELETE", "License o'chirildi.");
    }
    return Results.Json(new { ok = true });
});

app.MapGet("/api/admin/devices", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string search = context.Request.Query["q"].ToString();
    var rows = Db.GetDevices(dbPath, search);
    return Results.Json(new { ok = true, items = rows });
});

app.MapDelete("/api/admin/devices/{id:int}", (HttpContext context, int id) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    DeviceRow? before = Db.GetDeviceById(dbPath, id);
    var result = Db.DeleteDevice(dbPath, id);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "DEVICE_UNLINK", $"Qurilma unlink qilindi: #{id}, deviceId={before?.DeviceId ?? "-"}, license={before?.LicenseKey ?? "-"}");
    if (!string.IsNullOrWhiteSpace(before?.LicenseKey))
    {
        Db.AddLicenseHistoryByKey(dbPath, before.LicenseKey, "DEVICE_UNLINK", $"Qurilma unlink qilindi: {before.DeviceId}");
    }
    return Results.Json(new { ok = true });
});

app.MapGet("/api/admin/audit", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    int limit = 200;
    string rawLimit = context.Request.Query["limit"].ToString();
    if (int.TryParse(rawLimit, out int parsedLimit) && parsedLimit > 0 && parsedLimit <= 1000)
    {
        limit = parsedLimit;
    }

    var rows = Db.GetAuditLogs(dbPath, limit);
    return Results.Json(new { ok = true, items = rows });
});

app.MapGet("/api/admin/licenses/export.csv", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string search = context.Request.Query["q"].ToString();
    string status = context.Request.Query["status"].ToString();
    var rows = Db.GetLicenses(dbPath, search, status);
    var sb = new StringBuilder();
    sb.AppendLine("Id,LicenseKey,CustomerName,MaxDevices,Status,ExpiresAt");
    foreach (var r in rows)
    {
        sb.AppendLine($"{r.Id},{CsvEscape(r.LicenseKey)},{CsvEscape(r.CustomerName)},{r.MaxDevices},{CsvEscape(r.Status)},{CsvEscape(r.ExpiresAt ?? string.Empty)}");
    }

    return Results.Text(sb.ToString(), "text/csv; charset=utf-8");
});

app.MapGet("/api/admin/devices/export.csv", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string search = context.Request.Query["q"].ToString();
    var rows = Db.GetDevices(dbPath, search);
    var sb = new StringBuilder();
    sb.AppendLine("Id,LicenseKey,DeviceId,DeviceName,AppVersion,FirstSeenAt,LastSeenAt");
    foreach (var r in rows)
    {
        sb.AppendLine($"{r.Id},{CsvEscape(r.LicenseKey)},{CsvEscape(r.DeviceId)},{CsvEscape(r.DeviceName)},{CsvEscape(r.AppVersion)},{CsvEscape(r.FirstSeenAt)},{CsvEscape(r.LastSeenAt)}");
    }

    return Results.Text(sb.ToString(), "text/csv; charset=utf-8");
});

app.MapGet("/api/admin/licenses/{id:int}/history", (HttpContext context, int id) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    var rows = Db.GetLicenseHistory(dbPath, id, 300);
    return Results.Json(new { ok = true, items = rows });
});

app.MapGet("/api/admin/users", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var rows = Db.GetAdminUsers(dbPath);
    return Results.Json(new { ok = true, items = rows });
});

app.MapPost("/api/admin/users/create", (HttpContext context, CreateAdminUserRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string username = (req.Username ?? string.Empty).Trim();
    string password = req.Password ?? string.Empty;
    string role = string.Equals(req.Role, "Operator", StringComparison.OrdinalIgnoreCase) ? "Operator" : "SuperAdmin";
    if (username.Length < 3 || password.Length < 4)
    {
        return Results.BadRequest(new { ok = false, error = "Username/parol juda qisqa." });
    }

    var result = Db.CreateAdminUser(dbPath, username, password, role);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "ADMIN_USER_CREATE", $"Admin user yaratildi: {username}, role={role}");
    return Results.Json(new { ok = true });
});

app.MapPut("/api/admin/users/{id:int}/password", (HttpContext context, int id, UpdateAdminUserPasswordRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 4)
    {
        return Results.BadRequest(new { ok = false, error = "Yangi parol kamida 4 belgidan iborat bo'lsin." });
    }

    var result = Db.UpdateAdminPasswordById(dbPath, id, req.NewPassword);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "ADMIN_PASSWORD_UPDATE", $"Admin user #{id} paroli yangilandi.");
    return Results.Json(new { ok = true });
});

app.MapPost("/api/admin/reset-password", (HttpContext context, ResetMyPasswordRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string username = context.Items["admin_username"]?.ToString() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(req.OldPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
    {
        return Results.BadRequest(new { ok = false, error = "oldPassword/newPassword majburiy." });
    }
    if (req.NewPassword.Length < 4)
    {
        return Results.BadRequest(new { ok = false, error = "Yangi parol kamida 4 ta belgi bo'lsin." });
    }

    var auth = Db.AuthenticateAdmin(dbPath, username, req.OldPassword, adminUser, adminPass);
    if (auth == null)
    {
        return Results.BadRequest(new { ok = false, error = "Joriy parol noto'g'ri." });
    }

    var result = Db.UpdateAdminPasswordByUsername(dbPath, username, req.NewPassword);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "ADMIN_PASSWORD_RESET_SELF", $"Admin user {username} o'z parolini almashtirdi.");
    return Results.Json(new { ok = true });
});

app.MapPost("/api/admin/recovery/reset-password", (HttpContext context, RecoveryResetPasswordRequest req) =>
{
    string providedKey = context.Request.Headers["X-Recovery-Key"].ToString();
    if (!string.Equals(providedKey, recoveryKey, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 4)
    {
        return Results.BadRequest(new { ok = false, error = "username/newPassword noto'g'ri." });
    }

    var result = Db.UpdateAdminPasswordByUsername(dbPath, req.Username.Trim(), req.NewPassword);
    if (!result.Ok)
    {
        return Results.BadRequest(new { ok = false, error = result.Error });
    }

    Db.AddAuditLog(dbPath, "ADMIN_PASSWORD_RECOVERY", $"Recovery reset bajarildi: {req.Username.Trim()}");
    return Results.Json(new { ok = true });
});

app.MapGet("/api/version/latest", () =>
{
    var cfg = Db.GetAppVersionConfig(dbPath);
    return Results.Json(new { ok = true, version = cfg.Version, url = cfg.Url, note = cfg.Note, mandatory = cfg.Mandatory, sha256 = cfg.Sha256 });
});

app.MapGet("/api/contact/info", () =>
{
    string contact = Db.GetSupportContactInfo(dbPath);
    return Results.Json(new { ok = true, contact });
});

app.MapGet("/api/admin/contact/info", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }

    string contact = Db.GetSupportContactInfo(dbPath);
    return Results.Json(new { ok = true, contact });
});

app.MapPut("/api/admin/contact/info", (HttpContext context, SetSupportContactRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string contact = req.Contact?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(contact))
    {
        return Results.BadRequest(new { ok = false, error = "contact bo'sh bo'lmasin." });
    }

    Db.SetSupportContactInfo(dbPath, contact);
    Db.AddAuditLog(dbPath, "SUPPORT_CONTACT_SET", "Mijozlar uchun aloqa ma'lumoti yangilandi.");
    return Results.Json(new { ok = true });
});

app.MapPut("/api/admin/version", (HttpContext context, SetAppVersionRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(req.Version))
    {
        return Results.BadRequest(new { ok = false, error = "version bo'sh bo'lmasin." });
    }

    string sha256 = (req.Sha256 ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(sha256))
    {
        if (sha256.Length != 64 || sha256.Any(ch => !Uri.IsHexDigit(ch)))
        {
            return Results.BadRequest(new { ok = false, error = "sha256 64 belgili hex bo'lishi kerak." });
        }
        sha256 = sha256.ToUpperInvariant();
    }

    Db.SetAppVersionConfig(dbPath, req.Version.Trim(), req.Url?.Trim() ?? string.Empty, req.Note?.Trim() ?? string.Empty, req.Mandatory, sha256);
    Db.AddAuditLog(dbPath, "APP_VERSION_SET", $"Yangi version e'lon qilindi: {req.Version.Trim()}");
    return Results.Json(new { ok = true });
});

app.MapGet("/api/admin/client/first-login-credentials", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string licenseKey = context.Request.Query["licenseKey"].ToString();
    string deviceId = context.Request.Query["deviceId"].ToString();
    int limit = 200;
    if (int.TryParse(context.Request.Query["limit"], out int parsedLimit) && parsedLimit > 0 && parsedLimit <= 1000)
    {
        limit = parsedLimit;
    }

    var items = Db.GetClientFirstLoginCredentials(dbPath, licenseKey, deviceId, limit);
    return Results.Json(new { ok = true, items });
});

app.MapGet("/api/admin/backups", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var items = Directory.GetFiles(backupDir, "*.db")
        .Select(path => new FileInfo(path))
        .OrderByDescending(f => f.LastWriteTimeUtc)
        .Select(f => new BackupItem(f.Name, f.Length, f.LastWriteTimeUtc.ToString("O")))
        .ToList();
    return Results.Json(new { ok = true, items });
});

app.MapPost("/api/admin/backups/create", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string fileName = $"license_admin_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
    string fullPath = Path.Combine(backupDir, fileName);
    File.Copy(dbPath, fullPath, true);
    Db.AddAuditLog(dbPath, "BACKUP_CREATE", $"Backup yaratildi: {fileName}");
    return Results.Json(new { ok = true, fileName });
});

app.MapGet("/api/admin/backups/{fileName}", (HttpContext context, string fileName) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string safeName = Path.GetFileName(fileName);
    string fullPath = Path.Combine(backupDir, safeName);
    if (!File.Exists(fullPath))
    {
        return Results.NotFound(new { ok = false, error = "Backup topilmadi." });
    }

    return Results.File(fullPath, "application/octet-stream", safeName);
});

app.MapPost("/api/admin/backups/restore", (HttpContext context, RestoreBackupRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    if (string.IsNullOrWhiteSpace(req.FileName))
    {
        return Results.BadRequest(new { ok = false, error = "fileName majburiy." });
    }

    string safeName = Path.GetFileName(req.FileName.Trim());
    string fullPath = Path.Combine(backupDir, safeName);
    if (!File.Exists(fullPath))
    {
        return Results.BadRequest(new { ok = false, error = "Backup fayl topilmadi." });
    }

    File.Copy(fullPath, dbPath, true);
    Db.Initialize(dbPath);
    Db.EnsureDefaultAdmin(dbPath, adminUser, adminPass);
    Db.AddAuditLog(dbPath, "BACKUP_RESTORE", $"Backup restore qilindi: {safeName}");
    return Results.Json(new { ok = true });
});

app.MapGet("/api/admin/client/telemetry/users", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string licenseKey = context.Request.Query["licenseKey"].ToString();
    string deviceId = context.Request.Query["deviceId"].ToString();
    int limit = 200;
    if (int.TryParse(context.Request.Query["limit"], out int parsedLimit) && parsedLimit > 0 && parsedLimit <= 1000)
    {
        limit = parsedLimit;
    }

    var items = Db.GetClientUserTelemetry(dbPath, licenseKey, deviceId, limit);
    return Results.Json(new { ok = true, items });
});

app.MapGet("/api/admin/client/backups", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string licenseKey = context.Request.Query["licenseKey"].ToString();
    string deviceId = context.Request.Query["deviceId"].ToString();
    int limit = 200;
    if (int.TryParse(context.Request.Query["limit"], out int parsedLimit) && parsedLimit > 0 && parsedLimit <= 1000)
    {
        limit = parsedLimit;
    }

    var items = Db.GetClientBackupRows(dbPath, licenseKey, deviceId, limit);
    return Results.Json(new { ok = true, items });
});

app.MapGet("/api/admin/client/backups/{id:int}/download", (HttpContext context, int id) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    ClientBackupDownloadRow? row = Db.GetClientBackupDownloadById(dbPath, id);
    if (row == null)
    {
        return Results.NotFound(new { ok = false, error = "Backup topilmadi." });
    }

    string fileName = $"{row.LicenseKey}_{row.FileName}";
    if (row.FileData != null && row.FileData.Length > 0)
    {
        return Results.File(row.FileData, "application/octet-stream", fileName);
    }
    if (!string.IsNullOrWhiteSpace(row.StoredPath) && File.Exists(row.StoredPath))
    {
        return Results.File(row.StoredPath, "application/octet-stream", fileName);
    }
    return Results.NotFound(new { ok = false, error = "Backup fayli topilmadi." });
});

app.MapPost("/api/client/telemetry/users", (ClientUserTelemetryRequest req) =>
{
    string licenseKey = req.LicenseKey?.Trim() ?? string.Empty;
    string deviceId = req.DeviceId?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }

    if (!Db.IsClientAllowed(dbPath, licenseKey, deviceId))
    {
        return Results.Unauthorized();
    }

    int userCount = Math.Max(0, req.UserCount);
    string usernamesJson = JsonSerializer.Serialize(req.Usernames ?? new List<string>());
    Db.InsertClientUserTelemetry(dbPath, licenseKey, deviceId, req.AppVersion?.Trim() ?? "-", userCount, usernamesJson);
    Db.AddAuditLog(dbPath, "CLIENT_USER_TELEMETRY", $"Users sync: key={licenseKey}, device={deviceId}, count={userCount}");
    return Results.Json(new { ok = true });
});

app.MapPost("/api/client/backups/upload", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    string licenseKey = form["licenseKey"].ToString().Trim();
    string deviceId = form["deviceId"].ToString().Trim();
    string appVersion = form["appVersion"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }

    if (!Db.IsClientAllowed(dbPath, licenseKey, deviceId))
    {
        return Results.Unauthorized();
    }

    IFormFile? file = form.Files.FirstOrDefault();
    if (file == null || file.Length <= 0)
    {
        return Results.BadRequest(new { ok = false, error = "Backup fayli topilmadi." });
    }

    byte[] fileData;
    await using (var stream = new MemoryStream())
    {
        await file.CopyToAsync(stream);
        fileData = stream.ToArray();
    }

    Db.InsertClientBackup(dbPath, licenseKey, deviceId, appVersion, file.FileName, null, fileData, file.Length);
    Db.AddAuditLog(dbPath, "CLIENT_BACKUP_UPLOAD", $"Backup upload: key={licenseKey}, device={deviceId}, file={file.FileName}, size={file.Length}");
    return Results.Json(new { ok = true, file = file.FileName });
});

app.MapPost("/api/admin/client/password-resets/create", (HttpContext context, CreateClientPasswordResetRequest req) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string licenseKey = req.LicenseKey?.Trim() ?? string.Empty;
    string deviceId = req.DeviceId?.Trim() ?? string.Empty;
    string username = req.Username?.Trim() ?? string.Empty;
    string tempPassword = req.TempPassword ?? string.Empty;
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(tempPassword))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey, username, tempPassword majburiy." });
    }

    int id = Db.CreateClientPasswordReset(dbPath, licenseKey, string.IsNullOrWhiteSpace(deviceId) ? null : deviceId, username, tempPassword);
    Db.AddAuditLog(dbPath, "CLIENT_PASSWORD_RESET_CREATE", $"Client password reset yaratildi: id={id}, key={licenseKey}, device={deviceId}, username={username}");
    return Results.Json(new { ok = true, id });
});

app.MapGet("/api/admin/client/password-resets", (HttpContext context) =>
{
    if (!TryRequireApiAdmin(context, dbPath, adminUser, adminPass, out IResult? unauthorized))
    {
        return unauthorized!;
    }
    if (!IsSuperAdmin(context))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    string licenseKey = context.Request.Query["licenseKey"].ToString();
    string deviceId = context.Request.Query["deviceId"].ToString();
    string username = context.Request.Query["username"].ToString();
    string status = context.Request.Query["status"].ToString();
    int limit = 200;
    if (int.TryParse(context.Request.Query["limit"], out int parsedLimit) && parsedLimit > 0 && parsedLimit <= 1000)
    {
        limit = parsedLimit;
    }

    var items = Db.GetClientPasswordResets(dbPath, licenseKey, deviceId, username, status, limit);
    return Results.Json(new { ok = true, items });
});

app.MapGet("/api/client/password-resets/pending", (HttpContext context) =>
{
    string licenseKey = context.Request.Query["licenseKey"].ToString().Trim();
    string deviceId = context.Request.Query["deviceId"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }
    if (!Db.IsClientAllowed(dbPath, licenseKey, deviceId))
    {
        return Results.Unauthorized();
    }

    var items = Db.GetPendingClientPasswordResets(dbPath, licenseKey, deviceId, 50);
    return Results.Json(new { ok = true, items });
});

app.MapPost("/api/client/password-resets/{id:int}/ack", (int id, ClientPasswordResetAckRequest req) =>
{
    string licenseKey = req.LicenseKey?.Trim() ?? string.Empty;
    string deviceId = req.DeviceId?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }
    if (!Db.IsClientAllowed(dbPath, licenseKey, deviceId))
    {
        return Results.Unauthorized();
    }

    Db.AckClientPasswordReset(dbPath, id, licenseKey, deviceId, req.Applied, req.Note?.Trim() ?? string.Empty);
    return Results.Json(new { ok = true });
});

app.MapPost("/api/activate", (HttpContext context, ActivationRequest req) =>
{
    string rateKey = $"{context.Connection.RemoteIpAddress}|{req.LicenseKey?.Trim()}";
    if (IsRateLimited(activationAttempts, rateKey, 8, TimeSpan.FromMinutes(1)))
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    try
    {
        if (string.IsNullOrWhiteSpace(req.LicenseKey) || string.IsNullOrWhiteSpace(req.DeviceId))
        {
            return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
        }

        string normalizedLicenseKey = req.LicenseKey.Trim();
        string normalizedDeviceId = req.DeviceId.Trim();
        var result = Db.Activate(dbPath, normalizedLicenseKey, normalizedDeviceId, req.DeviceName?.Trim() ?? "-", req.AppVersion?.Trim() ?? "-");
        if (!result.Ok)
        {
            return Results.BadRequest(new { ok = false, error = result.Error });
        }

        Db.AddAuditLog(dbPath, "ACTIVATE_OK", $"Aktivatsiya: key={normalizedLicenseKey}, device={normalizedDeviceId}, expiresAt={result.ExpiresAt ?? "-"}");
        Db.AddLicenseHistoryByKey(dbPath, normalizedLicenseKey, "ACTIVATE", $"Qurilma aktivatsiya qilindi: {normalizedDeviceId}");
        ClientFirstLoginCredentialRow? firstLogin = Db.EnsureClientFirstLoginCredential(dbPath, normalizedLicenseKey, normalizedDeviceId);
        if (firstLogin != null)
        {
            Db.AddAuditLog(dbPath, "CLIENT_FIRST_LOGIN_ISSUED", $"Bir martalik login berildi: key={normalizedLicenseKey}, device={normalizedDeviceId}, user={firstLogin.Username}");
        }
        string supportContact = Db.GetSupportContactInfo(dbPath);

        string payloadJson = JsonSerializer.Serialize(new
        {
            license_key = normalizedLicenseKey,
            device_id = normalizedDeviceId,
            issued_at = DateTime.UtcNow.ToString("O"),
            expires_at = result.ExpiresAt
        });
        string signature = Sign(payloadJson, signingKey);
        return Results.Json(new
        {
            ok = true,
            token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)) + "." + signature,
            expires_at = result.ExpiresAt,
            support_contact = supportContact,
            first_login = firstLogin == null
                ? null
                : new
                {
                    id = firstLogin.Id,
                    username = firstLogin.Username,
                    password = firstLogin.TempPassword
                }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ACTIVATE_ERROR] {ex}");
        return Results.Json(new { ok = false, error = "Server ichki xatolik. Iltimos keyinroq qayta urinib ko'ring." }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/client/first-login/{id:int}/ack", (int id, ClientFirstLoginAckRequest req) =>
{
    string licenseKey = req.LicenseKey?.Trim() ?? string.Empty;
    string deviceId = req.DeviceId?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(licenseKey) || string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }
    if (!Db.IsClientAllowed(dbPath, licenseKey, deviceId))
    {
        return Results.Unauthorized();
    }

    Db.AckClientFirstLoginCredential(dbPath, id, licenseKey, deviceId, req.Applied, req.Note?.Trim() ?? string.Empty);
    return Results.Json(new { ok = true });
});

app.MapPost("/api/heartbeat", (HeartbeatRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.LicenseKey) || string.IsNullOrWhiteSpace(req.DeviceId))
    {
        return Results.BadRequest(new { ok = false, error = "licenseKey va deviceId majburiy." });
    }

    Db.Heartbeat(dbPath, req.LicenseKey.Trim(), req.DeviceId.Trim(), req.AppVersion?.Trim() ?? "-");
    return Results.Json(new { ok = true, at = DateTime.UtcNow.ToString("O") });
});

app.MapGet("/releases", async (HttpContext context) =>
{
    await ProxyReleaseAssetAsync(context, releaseProxyHttpClient, releaseProxyBaseUrl, "releases.stable.json");
});

app.MapGet("/releases/{**assetPath}", async (HttpContext context, string? assetPath) =>
{
    await ProxyReleaseAssetAsync(context, releaseProxyHttpClient, releaseProxyBaseUrl, assetPath);
});

app.Run();

static bool TryRequireApiAdmin(HttpContext context, string dbPath, string adminUser, string adminPass, out IResult? unauthorized)
{
    unauthorized = null;
    string auth = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        unauthorized = Results.Unauthorized();
        return false;
    }

    string encoded = auth["Basic ".Length..].Trim();
    string decoded;
    try
    {
        decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }
    catch
    {
        unauthorized = Results.Unauthorized();
        return false;
    }

    int idx = decoded.IndexOf(':');
    if (idx <= 0)
    {
        unauthorized = Results.Unauthorized();
        return false;
    }

    string user = decoded[..idx];
    string pass = decoded[(idx + 1)..];
    var admin = Db.AuthenticateAdmin(dbPath, user, pass, adminUser, adminPass);
    if (admin == null)
    {
        unauthorized = Results.Unauthorized();
        return false;
    }

    context.Items["admin_username"] = admin.Username;
    context.Items["admin_role"] = admin.Role;

    return true;
}

static bool IsSuperAdmin(HttpContext context)
{
    return string.Equals(context.Items["admin_role"]?.ToString(), "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}

static bool TryRequireWebAdmin(HttpContext context, ConcurrentDictionary<string, AdminAuth> sessions, out AdminAuth? auth, out IResult? unauthorized)
{
    auth = null;
    unauthorized = null;
    string sid = context.Request.Cookies[AdminSessionCookieName] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(sid) || !sessions.TryGetValue(sid, out AdminAuth? current))
    {
        unauthorized = Results.Redirect("/admin/login");
        return false;
    }

    auth = current;
    return true;
}

static string EnsureCsrfToken(HttpContext context)
{
    string token = context.Request.Cookies[CsrfCookieName] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(token))
    {
        return token;
    }

    token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    context.Response.Cookies.Append(CsrfCookieName, token, new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps
    });
    return token;
}

static bool ValidateCsrf(HttpContext context, IFormCollection form)
{
    string formToken = form["csrf"].ToString().Trim();
    string cookieToken = context.Request.Cookies[CsrfCookieName] ?? string.Empty;
    if (string.IsNullOrWhiteSpace(formToken) || string.IsNullOrWhiteSpace(cookieToken))
    {
        return false;
    }

    return string.Equals(formToken, cookieToken, StringComparison.Ordinal);
}

static string MaskSecret(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "-";
    }

    if (value.Length <= 2)
    {
        return new string('*', value.Length);
    }

    int stars = Math.Max(2, value.Length - 2);
    return value[..1] + new string('*', stars) + value[^1..];
}

static string? NormalizeDate(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return null;
    }

    return DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt)
        ? dt.ToString("yyyy-MM-dd")
        : null;
}

static string Sign(string payload, string key)
{
    byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
    byte[] keyBytes = Encoding.UTF8.GetBytes(key);
    using var hmac = new HMACSHA256(keyBytes);
    return Convert.ToHexString(hmac.ComputeHash(payloadBytes));
}

static string CsvEscape(string value)
{
    if (value.Contains('"'))
    {
        value = value.Replace("\"", "\"\"");
    }

    return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{value}\"" : value;
}

static bool IsProductionLikeEnvironment()
{
    string? asp = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    string? dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    string? railway = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT");

    return string.Equals(asp, "Production", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(dotnet, "Production", StringComparison.OrdinalIgnoreCase) ||
           !string.IsNullOrWhiteSpace(railway);
}

static string NormalizeReleaseProxyBaseUrl(string value)
{
    return (value ?? string.Empty).Trim().TrimEnd('/');
}

static bool IsSafeReleaseAssetPath(string path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return false;
    }

    if (path.Contains("..", StringComparison.Ordinal) || path.Contains('\\'))
    {
        return false;
    }

    foreach (char ch in path)
    {
        bool ok = char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' || ch == '/';
        if (!ok)
        {
            return false;
        }
    }

    return true;
}

static async Task ProxyReleaseAssetAsync(HttpContext context, HttpClient httpClient, string proxyBaseUrl, string? assetPath)
{
    if (string.IsNullOrWhiteSpace(proxyBaseUrl))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { ok = false, error = "Release proxy manzili sozlanmagan." });
        return;
    }

    string normalizedPath = (assetPath ?? string.Empty).Trim().TrimStart('/');
    if (string.IsNullOrWhiteSpace(normalizedPath))
    {
        normalizedPath = "releases.stable.json";
    }

    if (!IsSafeReleaseAssetPath(normalizedPath))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { ok = false, error = "Noto'g'ri release asset path." });
        return;
    }

    string targetUrl = $"{proxyBaseUrl}/{normalizedPath}";
    try
    {
        using var upstream = await httpClient.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)upstream.StatusCode;
        context.Response.ContentType = upstream.Content.Headers.ContentType?.ToString() ?? GuessReleaseContentType(normalizedPath);
        if (upstream.Content.Headers.ContentLength.HasValue)
        {
            context.Response.ContentLength = upstream.Content.Headers.ContentLength.Value;
        }
        if (upstream.Headers.ETag is not null)
        {
            context.Response.Headers.ETag = upstream.Headers.ETag.ToString();
        }
        if (upstream.Headers.CacheControl is not null)
        {
            context.Response.Headers.CacheControl = upstream.Headers.CacheControl.ToString();
        }

        await upstream.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[RELEASE_PROXY_ERROR] {targetUrl} => {ex.Message}");
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsJsonAsync(new { ok = false, error = "Release serverga ulanishda xato." });
    }
}

static string GuessReleaseContentType(string path)
{
    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
        return "application/json; charset=utf-8";
    }
    if (path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
    {
        return "application/octet-stream";
    }
    if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
    {
        return "application/zip";
    }
    if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
    {
        return "application/octet-stream";
    }

    return "application/octet-stream";
}

static bool IsRateLimited(ConcurrentDictionary<string, (int Count, DateTime WindowStartUtc)> store, string key, int limit, TimeSpan window)
{
    DateTime now = DateTime.UtcNow;
    (int Count, DateTime WindowStartUtc) next = store.AddOrUpdate(
        key,
        _ => (1, now),
        (_, old) =>
        {
            if ((now - old.WindowStartUtc) > window)
            {
                return (1, now);
            }

            return (old.Count + 1, old.WindowStartUtc);
        });

    return next.Count > limit;
}

static string NormalizePostgresConnectionString(string raw)
{
    string value = raw.Trim();
    if (value.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
    {
        return value;
    }

    if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
    {
        string userInfo = Uri.UnescapeDataString(uri.UserInfo ?? string.Empty);
        string username = string.Empty;
        string password = string.Empty;
        int separator = userInfo.IndexOf(':');
        if (separator >= 0)
        {
            username = userInfo[..separator];
            password = userInfo[(separator + 1)..];
        }
        else
        {
            username = userInfo;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = username,
            Password = password,
            Database = uri.AbsolutePath.Trim('/'),
            SslMode = SslMode.Require
        };

        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            string query = uri.Query.TrimStart('?');
            foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split('=', 2);
                string key = Uri.UnescapeDataString(parts[0]);
                string val = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<SslMode>(val, true, out var ssl))
                    {
                        builder.SslMode = ssl;
                    }
                }
                // Npgsql 8 da TrustServerCertificate parametri eskirgan, shuning uchun e'tiborsiz qoldiramiz.
            }
        }

        return builder.ConnectionString;
    }

    return value;
}

static class Db
{
    private static byte[] _secretKey = SHA256.HashData(Encoding.UTF8.GetBytes("default-license-secret"));

    public static void ConfigureSecrets(string signingKey, string recoveryKey)
    {
        string material = $"{signingKey}|{recoveryKey}";
        _secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    public static void Initialize(string dbPath)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
CREATE TABLE IF NOT EXISTS Licenses(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseKey TEXT NOT NULL UNIQUE,
    CustomerName TEXT NOT NULL,
    MaxDevices INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ExpiresAt TEXT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Devices(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseId INTEGER NOT NULL,
    DeviceId TEXT NOT NULL,
    DeviceName TEXT NULL,
    AppVersion TEXT NULL,
    FirstSeenAt TEXT NULL,
    LastSeenAt TEXT NULL,
    UNIQUE(LicenseId, DeviceId),
    FOREIGN KEY(LicenseId) REFERENCES Licenses(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Devices_LastSeenAt ON Devices(LastSeenAt);
CREATE TABLE IF NOT EXISTS AuditLogs(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    EventType TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt DESC);
CREATE TABLE IF NOT EXISTS AdminUsers(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS AppConfig(
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS LicenseHistory(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseId INTEGER NULL,
    LicenseKey TEXT NOT NULL,
    EventType TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);
CREATE INDEX IF NOT EXISTS IX_LicenseHistory_CreatedAt ON LicenseHistory(CreatedAt DESC);
CREATE TABLE IF NOT EXISTS ClientUserTelemetry(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseKey TEXT NOT NULL,
    DeviceId TEXT NOT NULL,
    AppVersion TEXT NOT NULL,
    UserCount INTEGER NOT NULL,
    UsernamesJson TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ClientUserTelemetry_LicenseKey ON ClientUserTelemetry(LicenseKey);
CREATE INDEX IF NOT EXISTS IX_ClientUserTelemetry_CreatedAt ON ClientUserTelemetry(CreatedAt DESC);
CREATE TABLE IF NOT EXISTS ClientBackups(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseKey TEXT NOT NULL,
    DeviceId TEXT NOT NULL,
    AppVersion TEXT NOT NULL,
    FileName TEXT NOT NULL,
    StoredPath TEXT NULL,
    FileData BYTEA NULL,
    SizeBytes INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ClientBackups_LicenseKey ON ClientBackups(LicenseKey);
CREATE INDEX IF NOT EXISTS IX_ClientBackups_CreatedAt ON ClientBackups(CreatedAt DESC);
CREATE TABLE IF NOT EXISTS ClientPasswordResets(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseKey TEXT NOT NULL,
    DeviceId TEXT NULL,
    Username TEXT NOT NULL,
    TempPassword TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    Note TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL,
    AppliedAt TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_ClientPasswordResets_LicenseKey ON ClientPasswordResets(LicenseKey);
CREATE INDEX IF NOT EXISTS IX_ClientPasswordResets_Status ON ClientPasswordResets(Status);
CREATE INDEX IF NOT EXISTS IX_ClientPasswordResets_CreatedAt ON ClientPasswordResets(CreatedAt DESC);
CREATE TABLE IF NOT EXISTS ClientFirstLoginCredentials(
    Id INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    LicenseKey TEXT NOT NULL,
    DeviceId TEXT NOT NULL,
    Username TEXT NOT NULL,
    TempPassword TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    Note TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL,
    AppliedAt TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_ClientFirstLoginCredentials_LicenseKey ON ClientFirstLoginCredentials(LicenseKey);
CREATE INDEX IF NOT EXISTS IX_ClientFirstLoginCredentials_Status ON ClientFirstLoginCredentials(Status);
CREATE INDEX IF NOT EXISTS IX_ClientFirstLoginCredentials_CreatedAt ON ClientFirstLoginCredentials(CreatedAt DESC);
""";
        cmd.ExecuteNonQuery();

        EnsureDevicesColumn(conn, "DeviceName", "TEXT");
        EnsureDevicesColumn(conn, "AppVersion", "TEXT");
        EnsureDevicesColumn(conn, "FirstSeenAt", "TEXT");
        EnsureDevicesColumn(conn, "LastSeenAt", "TEXT");

        using var fixNulls = conn.CreateCommand();
        fixNulls.CommandText = """
UPDATE Devices SET DeviceName = COALESCE(DeviceName, '-') ;
UPDATE Devices SET AppVersion = COALESCE(AppVersion, '-') ;
UPDATE Devices SET FirstSeenAt = COALESCE(FirstSeenAt, to_char((NOW() AT TIME ZONE 'UTC'),'YYYY-MM-DD HH24:MI:SS')) ;
UPDATE Devices SET LastSeenAt = COALESCE(LastSeenAt, to_char((NOW() AT TIME ZONE 'UTC'),'YYYY-MM-DD HH24:MI:SS')) ;
""";
        fixNulls.ExecuteNonQuery();

        using var ensureBackupsColumns = conn.CreateCommand();
        ensureBackupsColumns.CommandText = """
ALTER TABLE ClientBackups ADD COLUMN IF NOT EXISTS FileData BYTEA;
ALTER TABLE ClientBackups ALTER COLUMN StoredPath DROP NOT NULL;
""";
        ensureBackupsColumns.ExecuteNonQuery();
    }

    public static DashboardStats GetDashboardStats(string dbPath)
    {
        using var conn = Open(dbPath);
        return new DashboardStats
        {
            TotalLicenses = ScalarInt(conn, "SELECT COUNT(*) FROM Licenses"),
            ActiveLicenses = ScalarInt(conn, "SELECT COUNT(*) FROM Licenses WHERE Status='Active'"),
            TotalDevices = ScalarInt(conn, "SELECT COUNT(*) FROM Devices"),
            ActiveToday = ScalarInt(conn, "SELECT COUNT(*) FROM Devices WHERE LEFT(COALESCE(LastSeenAt, ''), 10)=to_char((NOW() AT TIME ZONE 'UTC'),'YYYY-MM-DD')")
        };
    }

    public static List<LicenseRow> GetLicenses(string dbPath, string? search = null, string? status = null)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        string normalizedStatus = string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase)
            ? "Blocked"
            : string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : string.Empty;

        var where = new List<string>();
        if (hasSearch)
        {
            where.Add("(LicenseKey LIKE @q OR CustomerName LIKE @q)");
            cmd.Parameters.AddWithValue("@q", $"%{search!.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            where.Add("Status=@status");
            cmd.Parameters.AddWithValue("@status", normalizedStatus);
        }

        string whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = "SELECT Id, LicenseKey, CustomerName, MaxDevices, Status, ExpiresAt FROM Licenses" + whereSql + " ORDER BY Id DESC";
        using var reader = cmd.ExecuteReader();
        var list = new List<LicenseRow>();
        while (reader.Read())
        {
            list.Add(new LicenseRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return list;
    }

    public static List<DeviceRow> GetDevices(string dbPath, string? search = null)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        bool hasLicenseId = HasColumn(conn, "Devices", "LicenseId");
        bool hasLicenseKey = HasColumn(conn, "Devices", "LicenseKey");
        string deviceLicenseExpr = hasLicenseKey ? "d.LicenseKey" : "'-'";
        string joinCondition = hasLicenseId
            ? "l.Id = d.LicenseId"
            : hasLicenseKey ? "l.LicenseKey = d.LicenseKey" : "1=0";
        cmd.CommandText = """
SELECT d.Id,
       COALESCE(l.LicenseKey, 
""" + deviceLicenseExpr + """
, '-') AS LicenseKey,
       d.DeviceId,
       COALESCE(d.DeviceName, '-') AS DeviceName,
       COALESCE(d.AppVersion, '-') AS AppVersion,
       COALESCE(d.FirstSeenAt, '-') AS FirstSeenAt,
       COALESCE(d.LastSeenAt, '-') AS LastSeenAt
FROM Devices d
LEFT JOIN Licenses l ON 
""" + joinCondition + "\n" + (hasSearch ? $"WHERE (COALESCE(l.LicenseKey, {deviceLicenseExpr}, '') LIKE @q OR d.DeviceId LIKE @q OR d.DeviceName LIKE @q)\n" : string.Empty) + """
ORDER BY d.LastSeenAt DESC
""";
        if (hasSearch)
        {
            cmd.Parameters.AddWithValue("@q", $"%{search!.Trim()}%");
        }

        using var reader = cmd.ExecuteReader();
        var list = new List<DeviceRow>();
        while (reader.Read())
        {
            list.Add(new DeviceRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return list;
    }

    public static LicenseRow? GetLicenseById(string dbPath, int id)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, LicenseKey, CustomerName, MaxDevices, Status, ExpiresAt FROM Licenses WHERE Id=@id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new LicenseRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }

    public static DeviceRow? GetDeviceById(string dbPath, int id)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        bool hasLicenseId = HasColumn(conn, "Devices", "LicenseId");
        bool hasLicenseKey = HasColumn(conn, "Devices", "LicenseKey");
        string deviceLicenseExpr = hasLicenseKey ? "d.LicenseKey" : "'-'";
        string joinCondition = hasLicenseId
            ? "l.Id = d.LicenseId"
            : hasLicenseKey ? "l.LicenseKey = d.LicenseKey" : "1=0";
        cmd.CommandText = """
SELECT d.Id,
       COALESCE(l.LicenseKey, 
""" + deviceLicenseExpr + """
, '-') AS LicenseKey,
       d.DeviceId,
       COALESCE(d.DeviceName, '-') AS DeviceName,
       COALESCE(d.AppVersion, '-') AS AppVersion,
       COALESCE(d.FirstSeenAt, '-') AS FirstSeenAt,
       COALESCE(d.LastSeenAt, '-') AS LastSeenAt
FROM Devices d
LEFT JOIN Licenses l ON 
""" + joinCondition + """
WHERE d.Id=@id
LIMIT 1
""";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new DeviceRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6));
    }

    public static string CreateLicense(string dbPath, string customerName, int maxDevices, string? expiresAt)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        string key = GenerateKey();
        cmd.CommandText = """
INSERT INTO Licenses(LicenseKey, CustomerName, MaxDevices, Status, ExpiresAt, CreatedAt)
VALUES(@key,@customer,@max,'Active',@expires,@created)
""";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@customer", customerName);
        cmd.Parameters.AddWithValue("@max", maxDevices);
        cmd.Parameters.AddWithValue("@expires", (object?)expiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
        return key;
    }

    public static string? ToggleLicense(string dbPath, int id)
    {
        using var conn = Open(dbPath);
        using var update = conn.CreateCommand();
        update.CommandText = "UPDATE Licenses SET Status=CASE WHEN Status='Active' THEN 'Blocked' ELSE 'Active' END WHERE Id=@id";
        update.Parameters.AddWithValue("@id", id);
        update.ExecuteNonQuery();

        using var select = conn.CreateCommand();
        select.CommandText = "SELECT Status FROM Licenses WHERE Id=@id LIMIT 1";
        select.Parameters.AddWithValue("@id", id);
        object? result = select.ExecuteScalar();
        return result?.ToString();
    }

    public static ActionResult UpdateLicense(string dbPath, int id, string licenseKey, string customerName, int maxDevices, string status, string? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return ActionResult.Failure("License key bo'sh bo'lmasligi kerak.");
        }

        try
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
UPDATE Licenses
SET LicenseKey=@key,
    CustomerName=@customer,
    MaxDevices=@max,
    Status=@status,
    ExpiresAt=@expires
WHERE Id=@id
""";
            cmd.Parameters.AddWithValue("@key", licenseKey);
            cmd.Parameters.AddWithValue("@customer", customerName);
            cmd.Parameters.AddWithValue("@max", maxDevices);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@expires", (object?)expiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);
            int affected = cmd.ExecuteNonQuery();
            if (affected <= 0)
            {
                return ActionResult.Failure("License topilmadi.");
            }

            return ActionResult.Success();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return ActionResult.Failure("Bunday license key allaqachon mavjud.");
        }
        catch (Exception ex)
        {
            return ActionResult.Failure(ex.Message);
        }
    }

    public static ActionResult DeleteLicense(string dbPath, int id)
    {
        try
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Licenses WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            int affected = cmd.ExecuteNonQuery();
            if (affected <= 0)
            {
                return ActionResult.Failure("License topilmadi.");
            }

            return ActionResult.Success();
        }
        catch (Exception ex)
        {
            return ActionResult.Failure(ex.Message);
        }
    }

    public static ActionResult DeleteDevice(string dbPath, int id)
    {
        try
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Devices WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            int affected = cmd.ExecuteNonQuery();
            if (affected <= 0)
            {
                return ActionResult.Failure("Qurilma topilmadi.");
            }

            return ActionResult.Success();
        }
        catch (Exception ex)
        {
            return ActionResult.Failure(ex.Message);
        }
    }

    public static ActivateResult Activate(string dbPath, string licenseKey, string deviceId, string deviceName, string appVersion)
    {
        using var conn = Open(dbPath);
        using var tx = conn.BeginTransaction();
        using var find = conn.CreateCommand();
        find.Transaction = tx;
        find.CommandText = "SELECT Id, Status, MaxDevices, ExpiresAt FROM Licenses WHERE LicenseKey=@key LIMIT 1";
        find.Parameters.AddWithValue("@key", licenseKey);
        int licenseId;
        string status;
        int maxDevices;
        string? expiresAt;
        using (var reader = find.ExecuteReader())
        {
            if (!reader.Read())
            {
                return new ActivateResult(false, "License topilmadi.");
            }

            licenseId = reader.GetInt32(0);
            status = reader.GetString(1);
            maxDevices = reader.GetInt32(2);
            expiresAt = reader.IsDBNull(3) ? null : reader.GetString(3);
        }
        if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new ActivateResult(false, "License bloklangan.");
        }

        if (!string.IsNullOrWhiteSpace(expiresAt) &&
            DateTime.TryParseExact(expiresAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exp) &&
            DateTime.UtcNow.Date > exp.Date)
        {
            return new ActivateResult(false, "License muddati tugagan.");
        }

        using var existsCmd = conn.CreateCommand();
        existsCmd.Transaction = tx;
        existsCmd.CommandText = "SELECT COUNT(*) FROM Devices WHERE LicenseId=@lid AND DeviceId=@did";
        existsCmd.Parameters.AddWithValue("@lid", licenseId);
        existsCmd.Parameters.AddWithValue("@did", deviceId);
        bool exists = Convert.ToInt32(existsCmd.ExecuteScalar()) > 0;
        if (!exists)
        {
            using var countCmd = conn.CreateCommand();
            countCmd.Transaction = tx;
            countCmd.CommandText = "SELECT COUNT(*) FROM Devices WHERE LicenseId=@lid";
            countCmd.Parameters.AddWithValue("@lid", licenseId);
            int currentCount = Convert.ToInt32(countCmd.ExecuteScalar());
            if (currentCount >= maxDevices)
            {
                return new ActivateResult(false, "Qurilmalar limiti tugagan.");
            }
        }

        string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        using var upsert = conn.CreateCommand();
        upsert.Transaction = tx;
        upsert.CommandText = """
INSERT INTO Devices(LicenseId, DeviceId, DeviceName, AppVersion, FirstSeenAt, LastSeenAt)
VALUES(@lid,@did,@dname,@ver,@now,@now)
ON CONFLICT(LicenseId, DeviceId)
DO UPDATE SET DeviceName=excluded.DeviceName, AppVersion=excluded.AppVersion, LastSeenAt=excluded.LastSeenAt
""";
        upsert.Parameters.AddWithValue("@lid", licenseId);
        upsert.Parameters.AddWithValue("@did", deviceId);
        upsert.Parameters.AddWithValue("@dname", deviceName);
        upsert.Parameters.AddWithValue("@ver", appVersion);
        upsert.Parameters.AddWithValue("@now", now);
        upsert.ExecuteNonQuery();

        tx.Commit();
        return new ActivateResult(true, null, expiresAt);
    }

    public static void Heartbeat(string dbPath, string licenseKey, string deviceId, string appVersion)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
UPDATE Devices
SET LastSeenAt=@now, AppVersion=@ver
WHERE DeviceId=@did
  AND LicenseId=(SELECT Id FROM Licenses WHERE LicenseKey=@key LIMIT 1)
""";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@ver", appVersion);
        cmd.Parameters.AddWithValue("@did", deviceId);
        cmd.Parameters.AddWithValue("@key", licenseKey);
        cmd.ExecuteNonQuery();
    }

    public static void AddAuditLog(string dbPath, string eventType, string message)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO AuditLogs(EventType, Message, CreatedAt)
VALUES(@eventType, @message, @createdAt)
""";
        cmd.Parameters.AddWithValue("@eventType", eventType);
        cmd.Parameters.AddWithValue("@message", message);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public static List<AuditRow> GetAuditLogs(string dbPath, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, EventType, Message, CreatedAt FROM AuditLogs ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<AuditRow>();
        while (reader.Read())
        {
            list.Add(new AuditRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return list;
    }

    public static bool IsClientAllowed(string dbPath, string licenseKey, string deviceId)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT COUNT(*)
FROM Devices d
INNER JOIN Licenses l ON l.Id = d.LicenseId
WHERE l.LicenseKey=@key AND d.DeviceId=@device
LIMIT 1
""";
        cmd.Parameters.AddWithValue("@key", licenseKey);
        cmd.Parameters.AddWithValue("@device", deviceId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public static void InsertClientUserTelemetry(string dbPath, string licenseKey, string deviceId, string appVersion, int userCount, string usernamesJson)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO ClientUserTelemetry(LicenseKey, DeviceId, AppVersion, UserCount, UsernamesJson, CreatedAt)
VALUES(@k, @d, @v, @c, @u, @created)
""";
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", deviceId);
        cmd.Parameters.AddWithValue("@v", appVersion);
        cmd.Parameters.AddWithValue("@c", userCount);
        cmd.Parameters.AddWithValue("@u", usernamesJson);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public static List<ClientUserTelemetryRow> GetClientUserTelemetry(string dbPath, string? licenseKey, string? deviceId, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            where.Add("LicenseKey=@k");
            cmd.Parameters.AddWithValue("@k", licenseKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            where.Add("DeviceId=@d");
            cmd.Parameters.AddWithValue("@d", deviceId.Trim());
        }

        string whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = "SELECT Id, LicenseKey, DeviceId, AppVersion, UserCount, UsernamesJson, CreatedAt FROM ClientUserTelemetry" + whereSql + " ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        var list = new List<ClientUserTelemetryRow>();
        while (reader.Read())
        {
            list.Add(new ClientUserTelemetryRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return list;
    }

    public static void InsertClientBackup(string dbPath, string licenseKey, string deviceId, string appVersion, string fileName, string? storedPath, byte[]? fileData, long sizeBytes)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO ClientBackups(LicenseKey, DeviceId, AppVersion, FileName, StoredPath, FileData, SizeBytes, CreatedAt)
VALUES(@k, @d, @v, @f, @p, @b, @s, @created)
""";
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", deviceId);
        cmd.Parameters.AddWithValue("@v", appVersion);
        cmd.Parameters.AddWithValue("@f", fileName);
        cmd.Parameters.AddWithValue("@p", (object?)storedPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@b", (object?)fileData ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@s", sizeBytes);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public static List<ClientBackupRow> GetClientBackupRows(string dbPath, string? licenseKey, string? deviceId, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            where.Add("LicenseKey=@k");
            cmd.Parameters.AddWithValue("@k", licenseKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            where.Add("DeviceId=@d");
            cmd.Parameters.AddWithValue("@d", deviceId.Trim());
        }

        string whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = "SELECT Id, LicenseKey, DeviceId, AppVersion, FileName, StoredPath, SizeBytes, CreatedAt FROM ClientBackups" + whereSql + " ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<ClientBackupRow>();
        while (reader.Read())
        {
            list.Add(new ClientBackupRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt64(6),
                reader.GetString(7)));
        }

        return list;
    }

    public static ClientBackupDownloadRow? GetClientBackupDownloadById(string dbPath, int id)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, LicenseKey, FileName, StoredPath, FileData FROM ClientBackups WHERE Id=@id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ClientBackupDownloadRow(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4));
    }

    public static int CreateClientPasswordReset(string dbPath, string licenseKey, string? deviceId, string username, string tempPassword)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO ClientPasswordResets(LicenseKey, DeviceId, Username, TempPassword, Status, Note, CreatedAt, AppliedAt)
VALUES(@k, @d, @u, @p, 'Pending', '', @created, NULL)
RETURNING Id;
""";
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", (object?)deviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", EncryptSecret(tempPassword));
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static List<ClientPasswordResetRow> GetClientPasswordResets(string dbPath, string? licenseKey, string? deviceId, string? username, string? status, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            where.Add("LicenseKey=@k");
            cmd.Parameters.AddWithValue("@k", licenseKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            where.Add("(DeviceId=@d OR DeviceId IS NULL)");
            cmd.Parameters.AddWithValue("@d", deviceId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            where.Add("Username=@u");
            cmd.Parameters.AddWithValue("@u", username.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            where.Add("Status=@s");
            cmd.Parameters.AddWithValue("@s", status.Trim());
        }

        string whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = "SELECT Id, LicenseKey, DeviceId, Username, TempPassword, Status, Note, CreatedAt, AppliedAt FROM ClientPasswordResets" + whereSql + " ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<ClientPasswordResetRow>();
        while (reader.Read())
        {
            list.Add(new ClientPasswordResetRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                DecryptSecret(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return list;
    }

    public static List<ClientPasswordResetClientRow> GetPendingClientPasswordResets(string dbPath, string licenseKey, string deviceId, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT Id, Username, TempPassword
FROM ClientPasswordResets
WHERE LicenseKey=@k
  AND Status='Pending'
  AND (DeviceId IS NULL OR DeviceId=@d)
ORDER BY Id ASC
LIMIT @limit
""";
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", deviceId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<ClientPasswordResetClientRow>();
        while (reader.Read())
        {
            list.Add(new ClientPasswordResetClientRow(
                reader.GetInt32(0),
                reader.GetString(1),
                DecryptSecret(reader.GetString(2))));
        }

        return list;
    }

    public static void AckClientPasswordReset(string dbPath, int id, string licenseKey, string deviceId, bool applied, string note)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
UPDATE ClientPasswordResets
SET Status=@status,
    Note=@note,
    AppliedAt=@at
WHERE Id=@id
  AND LicenseKey=@k
  AND (DeviceId IS NULL OR DeviceId=@d)
""";
        cmd.Parameters.AddWithValue("@status", applied ? "Applied" : "Failed");
        cmd.Parameters.AddWithValue("@note", note);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", deviceId);
        cmd.ExecuteNonQuery();
    }

    public static void EnsureDefaultAdmin(string dbPath, string username, string password)
    {
        using var conn = Open(dbPath);
        using var find = conn.CreateCommand();
        find.CommandText = "SELECT COUNT(*) FROM AdminUsers WHERE Username=@u";
        find.Parameters.AddWithValue("@u", username);
        int exists = Convert.ToInt32(find.ExecuteScalar());
        if (exists > 0)
        {
            return;
        }

        using var insert = conn.CreateCommand();
        insert.CommandText = """
INSERT INTO AdminUsers(Username, PasswordHash, Role, IsActive, CreatedAt)
VALUES(@u, @p, 'SuperAdmin', 1, @created)
""";
        insert.Parameters.AddWithValue("@u", username);
        insert.Parameters.AddWithValue("@p", HashPassword(password));
        insert.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        insert.ExecuteNonQuery();
    }

    public static AdminAuth? AuthenticateAdmin(string dbPath, string username, string password, string fallbackUser, string fallbackPass)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT Username, Role, IsActive, PasswordHash
FROM AdminUsers
WHERE Username=@u
LIMIT 1
""";
        cmd.Parameters.AddWithValue("@u", username);
        string? dbUser = null;
        string? role = null;
        bool active = false;
        string? hash = null;
        using (var reader = cmd.ExecuteReader())
        {
            if (reader.Read())
            {
                dbUser = reader.GetString(0);
                role = reader.GetString(1);
                active = reader.GetInt32(2) == 1;
                hash = reader.GetString(3);
            }
        }

        if (!string.IsNullOrWhiteSpace(dbUser))
        {
            if (!active || string.IsNullOrWhiteSpace(hash))
            {
                return null;
            }

            bool verified = VerifyPassword(password, hash, out bool needsUpgrade);
            if (!verified)
            {
                return null;
            }

            if (needsUpgrade)
            {
                using var upgrade = conn.CreateCommand();
                upgrade.CommandText = "UPDATE AdminUsers SET PasswordHash=@p WHERE Username=@u";
                upgrade.Parameters.AddWithValue("@p", HashPassword(password));
                upgrade.Parameters.AddWithValue("@u", dbUser);
                upgrade.ExecuteNonQuery();
            }

            return new AdminAuth(dbUser, role ?? "Admin");
        }

        if (string.Equals(username, fallbackUser, StringComparison.Ordinal) &&
            string.Equals(password, fallbackPass, StringComparison.Ordinal))
        {
            return new AdminAuth(username, "SuperAdmin");
        }

        return null;
    }

    public static List<AdminUserRow> GetAdminUsers(string dbPath)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, Role, IsActive, CreatedAt FROM AdminUsers ORDER BY Id DESC";
        using var reader = cmd.ExecuteReader();
        var list = new List<AdminUserRow>();
        while (reader.Read())
        {
            list.Add(new AdminUserRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) == 1,
                reader.GetString(4)));
        }

        return list;
    }

    public static ActionResult CreateAdminUser(string dbPath, string username, string password, string role)
    {
        try
        {
            using var conn = Open(dbPath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
INSERT INTO AdminUsers(Username, PasswordHash, Role, IsActive, CreatedAt)
VALUES(@u, @p, @r, 1, @created)
""";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", HashPassword(password));
            cmd.Parameters.AddWithValue("@r", role);
            cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
            return ActionResult.Success();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return ActionResult.Failure("Bunday username allaqachon mavjud.");
        }
        catch (Exception ex)
        {
            return ActionResult.Failure(ex.Message);
        }
    }

    public static ActionResult UpdateAdminPasswordById(string dbPath, int id, string newPassword)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AdminUsers SET PasswordHash=@p WHERE Id=@id";
        cmd.Parameters.AddWithValue("@p", HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@id", id);
        int affected = cmd.ExecuteNonQuery();
        return affected > 0 ? ActionResult.Success() : ActionResult.Failure("Admin user topilmadi.");
    }

    public static ActionResult UpdateAdminPasswordByUsername(string dbPath, string username, string newPassword)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE AdminUsers SET PasswordHash=@p WHERE Username=@u";
        cmd.Parameters.AddWithValue("@p", HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@u", username);
        int affected = cmd.ExecuteNonQuery();
        return affected > 0 ? ActionResult.Success() : ActionResult.Failure("Admin user topilmadi.");
    }

    public static void SetAppVersionConfig(string dbPath, string version, string url, string note, bool mandatory, string sha256 = "")
    {
        using var conn = Open(dbPath);
        SetConfig(conn, "app.version", version);
        SetConfig(conn, "app.update_url", url);
        SetConfig(conn, "app.update_note", note);
        SetConfig(conn, "app.update_mandatory", mandatory ? "1" : "0");
        SetConfig(conn, "app.update_sha256", sha256 ?? string.Empty);
    }

    public static AppVersionConfig GetAppVersionConfig(string dbPath)
    {
        using var conn = Open(dbPath);
        string version = GetConfig(conn, "app.version") ?? "1.0.0";
        string url = GetConfig(conn, "app.update_url") ?? string.Empty;
        string note = GetConfig(conn, "app.update_note") ?? string.Empty;
        bool mandatory = string.Equals(GetConfig(conn, "app.update_mandatory"), "1", StringComparison.Ordinal);
        string sha256 = (GetConfig(conn, "app.update_sha256") ?? string.Empty).Trim().ToUpperInvariant();
        return new AppVersionConfig(version, url, note, mandatory, sha256);
    }

    public static void EnsureClientDefaults(string dbPath, string supportContact, string firstLoginUsername)
    {
        using var conn = Open(dbPath);
        string currentContact = GetConfig(conn, "client.support_contact") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentContact))
        {
            SetConfig(conn, "client.support_contact", supportContact);
        }

        string currentUsername = GetConfig(conn, "client.first_login_username") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            SetConfig(conn, "client.first_login_username", firstLoginUsername);
        }
    }

    public static string GetSupportContactInfo(string dbPath)
    {
        using var conn = Open(dbPath);
        string? contact = GetConfig(conn, "client.support_contact");
        return string.IsNullOrWhiteSpace(contact) ? "-" : contact;
    }

    public static void SetSupportContactInfo(string dbPath, string contact)
    {
        using var conn = Open(dbPath);
        SetConfig(conn, "client.support_contact", contact);
    }

    public static ClientFirstLoginCredentialRow? EnsureClientFirstLoginCredential(string dbPath, string licenseKey, string deviceId)
    {
        using var conn = Open(dbPath);
        using var existingCmd = conn.CreateCommand();
        existingCmd.CommandText = """
SELECT Id, LicenseKey, DeviceId, Username, TempPassword, Status, Note, CreatedAt, AppliedAt
FROM ClientFirstLoginCredentials
WHERE LicenseKey=@k AND DeviceId=@d AND Status='Pending'
ORDER BY Id DESC
LIMIT 1
""";
        existingCmd.Parameters.AddWithValue("@k", licenseKey);
        existingCmd.Parameters.AddWithValue("@d", deviceId);
        using (var reader = existingCmd.ExecuteReader())
        {
            if (reader.Read())
            {
                return new ClientFirstLoginCredentialRow(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DecryptSecret(reader.GetString(4)),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8));
            }
        }

        string username = (GetConfig(conn, "client.first_login_username") ?? "admin").Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "admin";
        }

        string tempPassword = GenerateTemporaryPassword(10);
        string created = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        using var insert = conn.CreateCommand();
        insert.CommandText = """
INSERT INTO ClientFirstLoginCredentials(LicenseKey, DeviceId, Username, TempPassword, Status, Note, CreatedAt, AppliedAt)
VALUES(@k, @d, @u, @p, 'Pending', '', @created, NULL)
RETURNING Id;
""";
        insert.Parameters.AddWithValue("@k", licenseKey);
        insert.Parameters.AddWithValue("@d", deviceId);
        insert.Parameters.AddWithValue("@u", username);
        insert.Parameters.AddWithValue("@p", EncryptSecret(tempPassword));
        insert.Parameters.AddWithValue("@created", created);
        int id = Convert.ToInt32(insert.ExecuteScalar()!);
        return new ClientFirstLoginCredentialRow(id, licenseKey, deviceId, username, tempPassword, "Pending", string.Empty, created, null);
    }

    public static void AckClientFirstLoginCredential(string dbPath, int id, string licenseKey, string deviceId, bool applied, string note)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
UPDATE ClientFirstLoginCredentials
SET Status=@status,
    Note=@note,
    AppliedAt=@at
WHERE Id=@id
  AND LicenseKey=@k
  AND DeviceId=@d
""";
        cmd.Parameters.AddWithValue("@status", applied ? "Applied" : "Failed");
        cmd.Parameters.AddWithValue("@note", note);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@k", licenseKey);
        cmd.Parameters.AddWithValue("@d", deviceId);
        cmd.ExecuteNonQuery();
    }

    public static List<ClientFirstLoginCredentialRow> GetClientFirstLoginCredentials(string dbPath, string? licenseKey, string? deviceId, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            where.Add("LicenseKey=@k");
            cmd.Parameters.AddWithValue("@k", licenseKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            where.Add("DeviceId=@d");
            cmd.Parameters.AddWithValue("@d", deviceId.Trim());
        }

        string whereSql = where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty;
        cmd.CommandText = "SELECT Id, LicenseKey, DeviceId, Username, TempPassword, Status, Note, CreatedAt, AppliedAt FROM ClientFirstLoginCredentials" + whereSql + " ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        var list = new List<ClientFirstLoginCredentialRow>();
        while (reader.Read())
        {
            list.Add(new ClientFirstLoginCredentialRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DecryptSecret(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return list;
    }

    public static void AddLicenseHistoryById(string dbPath, int licenseId, string eventType, string message)
    {
        LicenseRow? row = GetLicenseById(dbPath, licenseId);
        if (row == null)
        {
            return;
        }

        AddLicenseHistory(dbPath, licenseId, row.LicenseKey, eventType, message);
    }

    public static void AddLicenseHistoryByKey(string dbPath, string licenseKey, string eventType, string message)
    {
        int? licenseId = null;
        using (var conn = Open(dbPath))
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id FROM Licenses WHERE LicenseKey=@k LIMIT 1";
            cmd.Parameters.AddWithValue("@k", licenseKey);
            object? id = cmd.ExecuteScalar();
            if (id != null && id != DBNull.Value)
            {
                licenseId = Convert.ToInt32(id);
            }
        }

        AddLicenseHistory(dbPath, licenseId, licenseKey, eventType, message);
    }

    public static List<LicenseHistoryRow> GetLicenseHistory(string dbPath, int licenseId, int limit)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
SELECT Id, LicenseId, LicenseKey, EventType, Message, CreatedAt
FROM LicenseHistory
WHERE LicenseId=@id
ORDER BY Id DESC
LIMIT @limit
""";
        cmd.Parameters.AddWithValue("@id", licenseId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        var list = new List<LicenseHistoryRow>();
        while (reader.Read())
        {
            list.Add(new LicenseHistoryRow(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return list;
    }

    private static void AddLicenseHistory(string dbPath, int? licenseId, string licenseKey, string eventType, string message)
    {
        using var conn = Open(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO LicenseHistory(LicenseId, LicenseKey, EventType, Message, CreatedAt)
VALUES(@id, @key, @eventType, @message, @created)
""";
        cmd.Parameters.AddWithValue("@id", (object?)licenseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@key", licenseKey);
        cmd.Parameters.AddWithValue("@eventType", eventType);
        cmd.Parameters.AddWithValue("@message", message);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    private static void SetConfig(NpgsqlConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
INSERT INTO AppConfig(Key, Value)
VALUES(@k, @v)
ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
""";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }

    private static string? GetConfig(NpgsqlConnection conn, string key)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppConfig WHERE Key=@k LIMIT 1";
        cmd.Parameters.AddWithValue("@k", key);
        object? result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    private static string EncryptSecret(string plain)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return string.Empty;
        }

        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
        byte[] cipher = new byte[plainBytes.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(_secretKey, 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        byte[] payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return "ENC1:" + Convert.ToBase64String(payload);
    }

    private static string DecryptSecret(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return string.Empty;
        }

        if (!stored.StartsWith("ENC1:", StringComparison.Ordinal))
        {
            return stored;
        }

        try
        {
            byte[] payload = Convert.FromBase64String(stored["ENC1:".Length..]);
            if (payload.Length < 12 + 16)
            {
                return string.Empty;
            }

            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            int cipherLen = payload.Length - nonce.Length - tag.Length;
            byte[] cipher = new byte[cipherLen];
            Buffer.BlockCopy(payload, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(payload, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(payload, nonce.Length + tag.Length, cipher, 0, cipherLen);

            byte[] plain = new byte[cipherLen];
            using var aes = new AesGcm(_secretKey, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string HashPassword(string password)
    {
        const int iterations = 120_000;
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"PBKDF2${iterations}${Convert.ToHexString(salt)}${Convert.ToHexString(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // New format: PBKDF2$iterations$saltHex$hashHex
        if (storedHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            string[] parts = storedHash.Split('$', StringSplitOptions.None);
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations) || iterations < 10_000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromHexString(parts[2]);
                byte[] expected = Convert.FromHexString(parts[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                    Encoding.UTF8.GetBytes(password),
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        // Legacy format: raw SHA256 hex (backward compatibility)
        byte[] legacy = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        string legacyHex = Convert.ToHexString(legacy);
        bool ok = string.Equals(legacyHex, storedHash, StringComparison.Ordinal);
        if (ok)
        {
            needsUpgrade = true;
        }
        return ok;
    }

    private static NpgsqlConnection Open(string dbPath)
    {
        var conn = new NpgsqlConnection(dbPath);
        conn.Open();
        return conn;
    }

    private static int ScalarInt(NpgsqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void EnsureDevicesColumn(NpgsqlConnection conn, string columnName, string type)
    {
        using var check = conn.CreateCommand();
        check.CommandText = """
SELECT 1
FROM information_schema.columns
WHERE table_schema='public' AND table_name='devices' AND lower(column_name)=lower(@column)
LIMIT 1
""";
        check.Parameters.AddWithValue("@column", columnName);
        using var reader = check.ExecuteReader();
        if (reader.Read())
        {
            return;
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE Devices ADD COLUMN IF NOT EXISTS {columnName} {type}";
        alter.ExecuteNonQuery();
    }

    private static bool HasColumn(NpgsqlConnection conn, string tableName, string columnName)
    {
        using var check = conn.CreateCommand();
        check.CommandText = """
SELECT 1
FROM information_schema.columns
WHERE table_schema='public' AND lower(table_name)=lower(@table) AND lower(column_name)=lower(@column)
LIMIT 1
""";
        check.Parameters.AddWithValue("@table", tableName);
        check.Parameters.AddWithValue("@column", columnName);
        using var reader = check.ExecuteReader();
        return reader.Read();
    }

    public static string TryMigrateFromSqlite(string pgConnString, string? sqlitePath)
    {
        if (string.IsNullOrWhiteSpace(sqlitePath) || !File.Exists(sqlitePath))
        {
            return "skipped";
        }

        using var pgConn = Open(pgConnString);
        if (ScalarInt(pgConn, "SELECT COUNT(*) FROM Licenses") > 0)
        {
            return "skipped";
        }

        using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sqlitePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        sqlite.Open();

        using var tx = pgConn.BeginTransaction();
        MigrateLicenses(sqlite, pgConn, tx);
        MigrateDevices(sqlite, pgConn, tx);
        MigrateAdminUsers(sqlite, pgConn, tx);
        MigrateAppConfig(sqlite, pgConn, tx);
        tx.Commit();

        ResetIdentity(pgConn, "licenses");
        ResetIdentity(pgConn, "devices");
        ResetIdentity(pgConn, "adminusers");
        return $"migrated from sqlite: {sqlitePath}";
    }

    private static void MigrateLicenses(SqliteConnection sqlite, NpgsqlConnection pgConn, NpgsqlTransaction tx)
    {
        if (!SqliteTableExists(sqlite, "Licenses"))
        {
            return;
        }

        using var read = sqlite.CreateCommand();
        read.CommandText = "SELECT Id, LicenseKey, CustomerName, MaxDevices, Status, ExpiresAt, CreatedAt FROM Licenses";
        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = pgConn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
INSERT INTO Licenses(Id, LicenseKey, CustomerName, MaxDevices, Status, ExpiresAt, CreatedAt)
VALUES(@id, @key, @customer, @max, @status, @expires, @created)
ON CONFLICT (LicenseKey) DO NOTHING
""";
            insert.Parameters.AddWithValue("@id", reader.GetInt32(0));
            insert.Parameters.AddWithValue("@key", reader.GetString(1));
            insert.Parameters.AddWithValue("@customer", reader.GetString(2));
            insert.Parameters.AddWithValue("@max", reader.GetInt32(3));
            insert.Parameters.AddWithValue("@status", reader.GetString(4));
            insert.Parameters.AddWithValue("@expires", reader.IsDBNull(5) ? DBNull.Value : reader.GetString(5));
            insert.Parameters.AddWithValue("@created", reader.IsDBNull(6) ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : reader.GetString(6));
            insert.ExecuteNonQuery();
        }
    }

    private static void MigrateDevices(SqliteConnection sqlite, NpgsqlConnection pgConn, NpgsqlTransaction tx)
    {
        if (!SqliteTableExists(sqlite, "Devices"))
        {
            return;
        }

        using var read = sqlite.CreateCommand();
        read.CommandText = "SELECT Id, LicenseId, DeviceId, DeviceName, AppVersion, FirstSeenAt, LastSeenAt FROM Devices";
        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = pgConn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
INSERT INTO Devices(Id, LicenseId, DeviceId, DeviceName, AppVersion, FirstSeenAt, LastSeenAt)
VALUES(@id, @licenseId, @deviceId, @name, @version, @firstSeen, @lastSeen)
ON CONFLICT (LicenseId, DeviceId) DO UPDATE
SET DeviceName=EXCLUDED.DeviceName,
    AppVersion=EXCLUDED.AppVersion,
    LastSeenAt=EXCLUDED.LastSeenAt
""";
            insert.Parameters.AddWithValue("@id", reader.GetInt32(0));
            insert.Parameters.AddWithValue("@licenseId", reader.GetInt32(1));
            insert.Parameters.AddWithValue("@deviceId", reader.GetString(2));
            insert.Parameters.AddWithValue("@name", reader.IsDBNull(3) ? "-" : reader.GetString(3));
            insert.Parameters.AddWithValue("@version", reader.IsDBNull(4) ? "-" : reader.GetString(4));
            insert.Parameters.AddWithValue("@firstSeen", reader.IsDBNull(5) ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : reader.GetString(5));
            insert.Parameters.AddWithValue("@lastSeen", reader.IsDBNull(6) ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : reader.GetString(6));
            insert.ExecuteNonQuery();
        }
    }

    private static void MigrateAdminUsers(SqliteConnection sqlite, NpgsqlConnection pgConn, NpgsqlTransaction tx)
    {
        if (!SqliteTableExists(sqlite, "AdminUsers"))
        {
            return;
        }

        using var read = sqlite.CreateCommand();
        read.CommandText = "SELECT Id, Username, PasswordHash, Role, IsActive, CreatedAt FROM AdminUsers";
        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = pgConn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
INSERT INTO AdminUsers(Id, Username, PasswordHash, Role, IsActive, CreatedAt)
VALUES(@id, @username, @hash, @role, @active, @createdAt)
ON CONFLICT (Username) DO NOTHING
""";
            insert.Parameters.AddWithValue("@id", reader.GetInt32(0));
            insert.Parameters.AddWithValue("@username", reader.GetString(1));
            insert.Parameters.AddWithValue("@hash", reader.GetString(2));
            insert.Parameters.AddWithValue("@role", reader.GetString(3));
            insert.Parameters.AddWithValue("@active", reader.GetInt32(4));
            insert.Parameters.AddWithValue("@createdAt", reader.IsDBNull(5) ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : reader.GetString(5));
            insert.ExecuteNonQuery();
        }
    }

    private static void MigrateAppConfig(SqliteConnection sqlite, NpgsqlConnection pgConn, NpgsqlTransaction tx)
    {
        if (!SqliteTableExists(sqlite, "AppConfig"))
        {
            return;
        }

        using var read = sqlite.CreateCommand();
        read.CommandText = "SELECT Key, Value FROM AppConfig";
        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = pgConn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
INSERT INTO AppConfig(Key, Value)
VALUES(@k, @v)
ON CONFLICT(Key) DO UPDATE SET Value=EXCLUDED.Value
""";
            insert.Parameters.AddWithValue("@k", reader.GetString(0));
            insert.Parameters.AddWithValue("@v", reader.GetString(1));
            insert.ExecuteNonQuery();
        }
    }

    private static bool SqliteTableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1";
        cmd.Parameters.AddWithValue("@name", tableName);
        return cmd.ExecuteScalar() != null;
    }

    private static void ResetIdentity(NpgsqlConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
SELECT setval(
    pg_get_serial_sequence('{tableName}', 'id'),
    GREATEST(COALESCE((SELECT MAX(id) FROM {tableName}), 0), 1),
    (SELECT COUNT(*) > 0 FROM {tableName})
);
""";
        cmd.ExecuteNonQuery();
    }

    private static string GenerateKey()
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[20];
        Span<byte> random = stackalloc byte[20];
        RandomNumberGenerator.Fill(random);
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = letters[random[i] % letters.Length];
        }

        string raw = new string(chars);
        return $"SRM-{raw[..5]}-{raw[5..10]}-{raw[10..15]}-{raw[15..20]}";
    }

    private static string GenerateTemporaryPassword(int length)
    {
        if (length < 8)
        {
            length = 8;
        }

        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%";
        char[] chars = new char[length];
        byte[] random = RandomNumberGenerator.GetBytes(length);
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = letters[random[i] % letters.Length];
        }

        return new string(chars);
    }
}

record ActivationRequest(string LicenseKey, string DeviceId, string? DeviceName, string? AppVersion);
record HeartbeatRequest(string LicenseKey, string DeviceId, string? AppVersion);
record CreateLicenseApiRequest(string CustomerName, int MaxDevices, string? ExpiresAt);
record UpdateLicenseApiRequest(string? LicenseKey, string CustomerName, int MaxDevices, string Status, string? ExpiresAt);
record CreateAdminUserRequest(string Username, string Password, string Role);
record UpdateAdminUserPasswordRequest(string NewPassword);
record ResetMyPasswordRequest(string OldPassword, string NewPassword);
record RecoveryResetPasswordRequest(string Username, string NewPassword);
record SetAppVersionRequest(string Version, string? Url, string? Note, bool Mandatory, string? Sha256);
record SetSupportContactRequest(string Contact);
record RestoreBackupRequest(string FileName);
record ClientUserTelemetryRequest(string LicenseKey, string DeviceId, string? AppVersion, int UserCount, List<string>? Usernames);
record CreateClientPasswordResetRequest(string LicenseKey, string? DeviceId, string Username, string TempPassword);
record ClientPasswordResetAckRequest(string LicenseKey, string DeviceId, bool Applied, string? Note);
record ClientFirstLoginAckRequest(string LicenseKey, string DeviceId, bool Applied, string? Note);
record DashboardStats
{
    public int TotalLicenses { get; init; }
    public int ActiveLicenses { get; init; }
    public int TotalDevices { get; init; }
    public int ActiveToday { get; init; }
}

record LicenseRow(int Id, string LicenseKey, string CustomerName, int MaxDevices, string Status, string? ExpiresAt);
record DeviceRow(int Id, string LicenseKey, string DeviceId, string DeviceName, string AppVersion, string FirstSeenAt, string LastSeenAt);
record AuditRow(int Id, string EventType, string Message, string CreatedAt);
record LicenseHistoryRow(int Id, int? LicenseId, string LicenseKey, string EventType, string Message, string CreatedAt);
record BackupItem(string FileName, long SizeBytes, string CreatedAtUtc);
record AdminAuth(string Username, string Role);
record AdminUserRow(int Id, string Username, string Role, bool IsActive, string CreatedAt);
record AppVersionConfig(string Version, string Url, string Note, bool Mandatory, string Sha256);
record ClientUserTelemetryRow(int Id, string LicenseKey, string DeviceId, string AppVersion, int UserCount, string UsernamesJson, string CreatedAt);
record ClientBackupRow(int Id, string LicenseKey, string DeviceId, string AppVersion, string FileName, string? StoredPath, long SizeBytes, string CreatedAt);
record ClientBackupDownloadRow(int Id, string LicenseKey, string FileName, string? StoredPath, byte[]? FileData);
record ClientPasswordResetRow(int Id, string LicenseKey, string? DeviceId, string Username, string TempPassword, string Status, string Note, string CreatedAt, string? AppliedAt);
record ClientPasswordResetClientRow(int Id, string Username, string TempPassword);
record ClientFirstLoginCredentialRow(int Id, string LicenseKey, string DeviceId, string Username, string TempPassword, string Status, string Note, string CreatedAt, string? AppliedAt);
record ActivateResult(bool Ok, string? Error, string? ExpiresAt = null);
record ActionResult(bool Ok, string? Error = null)
{
    public static ActionResult Success() => new ActionResult(true, null);
    public static ActionResult Failure(string error) => new ActionResult(false, error);
}




